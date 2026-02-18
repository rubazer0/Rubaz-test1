using MainCore.Commands.NextExecute;
using MainCore.Tasks.Base;
using MainCore.Helpers;
using MainCore.Parsers;
using MainCore.Infrasturecture.Persistence;
using Microsoft.EntityFrameworkCore;
using MainCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using OpenQA.Selenium; // Necessário para identificar o erro de Selenium

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
            ICustomServiceScopeFactory scopeFactory,
            UpdateBuildingCommand.Handler updateBuildingCommand,
            ToDorfCommand.Handler toDorfCommand,
            NextExecuteUpdateVillageTaskCommand.Handler nextExecuteUpdateVillageTaskCommand,
            CancellationToken cancellationToken)
        {
            var url = browser.CurrentUrl;
            Result result = Result.Ok();

            bool isFailed;
            IReadOnlyList<IError> errors;

            // --- BLOCO DE NAVEGAÇÃO BLINDADO ---
            try
            {
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
            }
            catch (WebDriverTimeoutException)
            {
                // Se der timeout (3 minutos), apenas falha a tarefa mas não fecha o bot
                return Result.Fail(new Error("Timeout: A página demorou muito para responder."));
            }
            catch (Exception ex)
            {
                // Qualquer outro erro de navegador
                return Result.Fail(new Error($"Erro ao atualizar vila: {ex.Message}"));
            }

            // --- CÓDIGO DE NOTIFICAÇÃO (TELEGRAM) ---
            try
            {
                var doc = browser.Html;

                // Cria escopo seguro para acessar banco de dados
                string villageName = "Vila Desconhecida";
                using (var scope = scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var village = await context.Villages
                        .FirstOrDefaultAsync(x => x.Id == task.VillageId.Value, cancellationToken);
                    if (village != null) villageName = village.Name;
                }

                // 1. Checar Ataque
                var attacks = MovementsParser.GetIncomingAttacks(doc);
                if (attacks.Any())
                {
                    var proximoAtaque = attacks.OrderBy(x => x).First();
                    string eta = proximoAtaque.ToString(@"hh\:mm\:ss");
                    await TelegramHelper.SendMessage(task.AccountId, $"⚠️ ATAQUE A CAMINHO: {attacks.Count} ataque(s) detectado(s) na vila {villageName}. O mais próximo chega em: {eta}");
                }

                // 2. Checar Celeiro
                long cerealAtual = StorageParser.GetCrop(doc);
                long capacidadeCeleiro = StorageParser.GetGranaryCapacity(doc);

                if (cerealAtual >= 0 && capacidadeCeleiro > 0)
                {
                    double porcentagem = (double)cerealAtual / capacidadeCeleiro * 100;

                    if (porcentagem <= 20)
                    {
                        await TelegramHelper.SendMessage(task.AccountId, $"📉 CEREAL BAIXO: A vila {villageName} está com o celeiro em {porcentagem:F1}%. ({cerealAtual}/{capacidadeCeleiro})");
                    }
                }
            }
            catch (Exception)
            {
                // Ignora falhas no Telegram para não parar o Farm
            }
            // --- FIM DO CÓDIGO DE NOTIFICAÇÃO ---

            await nextExecuteUpdateVillageTaskCommand.HandleAsync(new(task), cancellationToken);
            return Result.Ok();
        }
    }
}
