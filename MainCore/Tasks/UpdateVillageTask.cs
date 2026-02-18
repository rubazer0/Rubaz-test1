using MainCore.Commands.NextExecute;
using MainCore.Tasks.Base;
using MainCore.Helpers;
using MainCore.Parsers;
using MainCore.Infrasturecture.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MainCore.Tasks
{
    [Handler]
    public static partial class UpdateVillageTask
    {
        public sealed class Task : VillageTask
        {
            public Task(AccountId accountId, VillageId villageId) : base(accountId, villageId)
            {
            }

            protected override string TaskName => "Update village";

            public override bool CanStart(AppDbContext context)
            {
                var settingEnable = context.BooleanByName(VillageId, VillageSettingEnums.AutoRefreshEnable);
                if (!settingEnable) return false;

                return true;
            }
        }

        private static async ValueTask<Result> HandleAsync(
            Task task,
            IChromeBrowser browser,
            AppDbContext context,
            UpdateBuildingCommand.Handler updateBuildingCommand,
            ToDorfCommand.Handler toDorfCommand,
            NextExecuteUpdateVillageTaskCommand.Handler nextExecuteUpdateVillageTaskCommand,
            CancellationToken cancellationToken)
        {
            var url = browser.CurrentUrl;
            Result result;

            bool isFailed;
            IReadOnlyList<IError> errors;

            // --- Bloco Original de Atualização ---
            if (url.Contains("dorf1"))
            {
                (_, isFailed, errors) = await updateBuildingCommand.HandleAsync(new(task.VillageId), cancellationToken);
                if (isFailed) return Result.Fail(errors);
            }
            else if (url.Contains("dorf2"))
            {
                (_, isFailed, errors) = await updateBuildingCommand.HandleAsync(new(task.VillageId), cancellationToken);
                if (isFailed) return Result.Fail(errors);

                result = await toDorfCommand.HandleAsync(new(1), cancellationToken);
                if (result.IsFailed) return result;

                (_, isFailed, errors) = await updateBuildingCommand.HandleAsync(new(task.VillageId), cancellationToken);
                if (isFailed) return Result.Fail(errors);
            }
            else
            {
                result = await toDorfCommand.HandleAsync(new(1), cancellationToken);
                if (result.IsFailed) return result;

                (_, isFailed, errors) = await updateBuildingCommand.HandleAsync(new(task.VillageId), cancellationToken);
                if (isFailed) return Result.Fail(errors);
            }

            // --- INICIO DO CODIGO DE NOTIFICACAO (TELEGRAM) ---
            try
            {
                // Buscamos apenas o NOME da vila no banco
                var village = await context.Villages
                    .FirstOrDefaultAsync(x => x.Id == task.VillageId.Value, cancellationToken);

                if (village != null)
                {
                    // Pegamos o HTML da tela atual
                    var doc = browser.Html;

                    // 1. Checar Ataque (Usa MovementsParser)
                    var attacks = MovementsParser.GetIncomingAttacks(doc);
                    if (attacks.Any())
                    {
                        var proximoAtaque = attacks.OrderBy(x => x).First();
                        string eta = proximoAtaque.ToString(@"hh\:mm\:ss");
                        await TelegramHelper.SendMessage(task.AccountId, $"⚠️ ATAQUE A CAMINHO: {attacks.Count} ataque(s) detectado(s) na vila {village.Name}. O mais próximo chega em: {eta}");
                    }

                    // 2. Checar Celeiro (AGORA USA O STORAGEPARSER CORRETO)
                    long cerealAtual = StorageParser.GetCrop(doc);
                    long capacidadeCeleiro = StorageParser.GetGranaryCapacity(doc);

                    // Só verifica se conseguiu ler os dados corretamente (maior que 0)
                    if (cerealAtual >= 0 && capacidadeCeleiro > 0)
                    {
                        double porcentagem = (double)cerealAtual / capacidadeCeleiro * 100;

                        // Configurado para notificar se for MENOR OU IGUAL A 20%
                        if (porcentagem <= 20)
                        {
                            await TelegramHelper.SendMessage(task.AccountId, $"📉 CEREAL BAIXO: A vila {village.Name} está com o celeiro em {porcentagem:F1}%. ({cerealAtual}/{capacidadeCeleiro})");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignora erros silenciosamente
            }
            // --- FIM DO CODIGO DE NOTIFICACAO ---

            await nextExecuteUpdateVillageTaskCommand.HandleAsync(new(task), cancellationToken);
            return Result.Ok();
        }
    }
}
