using MainCore.Commands.UI.Misc;
using MainCore.UI.Models.Input;
using MainCore.UI.Models.Output;
using MainCore.UI.ViewModels.Abstract;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using ReactiveUI;
using MainCore.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reactive;

namespace MainCore.UI.ViewModels.Tabs
{
    [RegisterSingleton<AccountSettingViewModel>]
    public partial class AccountSettingViewModel : AccountTabViewModelBase
    {
        public AccountSettingInput AccountSettingInput { get; } = new();

        // --- CORREÇÃO: Inicializando com valor vazio (= "") ---
        private string _telegramToken = "";
        public string TelegramToken
        {
            get => _telegramToken;
            set => this.RaiseAndSetIfChanged(ref _telegramToken, value);
        }

        private string _telegramChatId = "";
        public string TelegramChatId
        {
            get => _telegramChatId;
            set => this.RaiseAndSetIfChanged(ref _telegramChatId, value);
        }
        // ---------------------------------------

        private readonly IDialogService _dialogService;
        private readonly IValidator<AccountSettingInput> _accountsettingInputValidator;
        private readonly ICustomServiceScopeFactory _serviceScopeFactory;

        public AccountSettingViewModel(IDialogService dialogService, IValidator<AccountSettingInput> accountsettingInputValidator, ICustomServiceScopeFactory serviceScopeFactory)
        {
            _dialogService = dialogService;
            _accountsettingInputValidator = accountsettingInputValidator;
            _serviceScopeFactory = serviceScopeFactory;

            LoadSettingsCommand.Subscribe(AccountSettingInput.Set);
        }

        protected override async Task Load(AccountId accountId)
        {
            // 1. Carrega as configurações normais do bot
            await LoadSettingsCommand.Execute(accountId);

            // 2. Carrega as configurações do Telegram do arquivo
            var telegramSettings = TelegramHelper.GetSettings(accountId);
            TelegramToken = telegramSettings.BotToken;
            TelegramChatId = telegramSettings.ChatId;
        }

        [ReactiveCommand]
        private async Task Save()
        {
            var result = await _accountsettingInputValidator.ValidateAsync(AccountSettingInput);
            if (!result.IsValid)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Error", result.ToString()));
                return;
            }

            // 1. Salva as configurações normais no banco de dados
            using var scope = _serviceScopeFactory.CreateScope(AccountId);
            var saveAccountSettingCommand = scope.ServiceProvider.GetRequiredService<SaveAccountSettingCommand.Handler>();
            await saveAccountSettingCommand.HandleAsync(new(AccountId, AccountSettingInput.Get()));

            // 2. Salva as configurações do Telegram no arquivo JSON
            TelegramHelper.SaveSettings(AccountId, TelegramToken, TelegramChatId);

            await _dialogService.MessageBox.Handle(new MessageBoxData("Information", "Settings saved."));
        }

        [ReactiveCommand]
        private async Task Import()
        {
            var path = await _dialogService.OpenFileDialog.Handle(Unit.Default);
            Dictionary<AccountSettingEnums, int> settings;
            try
            {
                var jsonString = await File.ReadAllTextAsync(path);
                settings = JsonSerializer.Deserialize<Dictionary<AccountSettingEnums, int>>(jsonString)!;
            }
            catch
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "Invalid file."));
                return;
            }

            AccountSettingInput.Set(settings);
            var result = await _accountsettingInputValidator.ValidateAsync(AccountSettingInput);
            if (!result.IsValid)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Error", result.ToString()));
                return;
            }

            using var scope = _serviceScopeFactory.CreateScope(AccountId);
            var saveAccountSettingCommand = scope.ServiceProvider.GetRequiredService<SaveAccountSettingCommand.Handler>();
            await saveAccountSettingCommand.HandleAsync(new(AccountId, AccountSettingInput.Get()));

            await _dialogService.MessageBox.Handle(new MessageBoxData("Information", "Settings imported."));
        }

        [ReactiveCommand]
        private async Task Export()
        {
            var path = await _dialogService.SaveFileDialog.Handle(Unit.Default);
            if (string.IsNullOrEmpty(path)) return;

            using var scope = _serviceScopeFactory.CreateScope(AccountId);
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = context.AccountsSetting
              .Where(x => x.AccountId == AccountId.Value)
              .ToDictionary(x => x.Setting, x => x.Value);

            var jsonString = JsonSerializer.Serialize(settings);
            await File.WriteAllTextAsync(path, jsonString);
            await _dialogService.MessageBox.Handle(new MessageBoxData("Information", "Settings exported."));
        }

        [ReactiveCommand]
        private Dictionary<AccountSettingEnums, int> LoadSettings(AccountId accountId)
        {
            using var scope = _serviceScopeFactory.CreateScope(AccountId);
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = context.AccountsSetting
              .Where(x => x.AccountId == AccountId.Value)
              .ToDictionary(x => x.Setting, x => x.Value);
            return settings;
        }

        [ReactiveCommand]
        private async Task TestTelegram()
        {
            // 1. Valida se tem algo escrito
            if (string.IsNullOrEmpty(TelegramToken) || string.IsNullOrEmpty(TelegramChatId))
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Erro", "Preencha o Token e o Chat ID antes de testar."));
                return;
            }

            try
            {
                // 2. Tenta enviar a mensagem de teste usando os dados da tela
                await TelegramHelper.TestSettings(TelegramToken, TelegramChatId);

                // 3. Se não deu erro, salva automaticamente para garantir
                TelegramHelper.SaveSettings(AccountId, TelegramToken, TelegramChatId);

                await _dialogService.MessageBox.Handle(new MessageBoxData("Sucesso", "Mensagem enviada! Verifique seu Telegram."));
            }
            catch (Exception ex)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Falha no Teste", $"Não foi possível enviar:\n{ex.Message}\n\nVerifique se o Token está correto e se você iniciou uma conversa com o Bot."));
            }
        }
    }
}
