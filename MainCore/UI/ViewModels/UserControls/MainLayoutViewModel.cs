using MainCore.Commands.UI.MainLayoutViewModel;
using MainCore.UI.Models.Output;
using MainCore.UI.Stores;
using MainCore.UI.ViewModels.Abstract;
using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Concurrency;
using System.Reflection;

namespace MainCore.UI.ViewModels.UserControls
{
    [RegisterSingleton<MainLayoutViewModel>]
    public partial class MainLayoutViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private readonly ICustomServiceScopeFactory _serviceScopeFactory;
        private readonly ITaskManager _taskManager;
        private readonly ILogger _logger;

        private readonly IRxQueue _rxQueue;

        private readonly AccountTabStore _accountTabStore;
        public ListBoxItemViewModel Accounts { get; } = new();
        public AccountTabStore AccountTabStore => _accountTabStore;

        private IObservable<bool> _canExecute;

        public MainLayoutViewModel(AccountTabStore accountTabStore, SelectedItemStore selectedItemStore, IDialogService dialogService, ITaskManager taskManager, ICustomServiceScopeFactory serviceScopeFactory, ILogger logger, IRxQueue rxQueue)
        {
            _accountTabStore = accountTabStore;
            _dialogService = dialogService;
            _serviceScopeFactory = serviceScopeFactory;
            _rxQueue = rxQueue;
            _logger = logger.ForContext<MainLayoutViewModel>();

            _taskManager = taskManager;

            _canExecute = this.WhenAnyValue(x => x.Accounts.IsEnable);

            var accountObservable = this.WhenAnyValue(x => x.Accounts.SelectedItem);
            accountObservable.BindTo(selectedItemStore, vm => vm.Account);

            accountObservable.Subscribe(x =>
            {
                var tabType = AccountTabType.Normal;
                if (x is null) tabType = AccountTabType.NoAccount;
                _accountTabStore.SetTabType(tabType);
            });

            accountObservable
                .WhereNotNull()
                .Select(x => new AccountId(x.Id))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .InvokeCommand(GetStatusCommand);

            _versionHelper = LoadVersionCommand
                .Do(version => _logger.Information("===============> Current version: {Version} <===============", version))
                .ToProperty(this, x => x.Version);

            LoadAccountCommand.Subscribe(Accounts.Load);

            GetStatusCommand.Subscribe(SetPauseText);

            Observable
                .Merge(
                    LoginCommand.IsExecuting.Select(x => !x),
                    LogoutCommand.IsExecuting.Select(x => !x),
                    PauseCommand.IsExecuting.Select(x => !x),
                    RestartCommand.IsExecuting.Select(x => !x)
                )
                .BindTo(Accounts, x => x.IsEnable);

            rxQueue.RegisterCommand<StatusModified>(StatusModifiedCommand);

            rxQueue.GetObservable<AccountsModified>()
                .Select(x => Unit.Default)
               .InvokeCommand(LoadAccountCommand);

            Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(5), RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateOnlineTime());
        }

        public async Task Load()
        {
            await LoadVersionCommand.Execute();
            await LoadAccountCommand.Execute();
        }

        [ReactiveCommand]
        private void StatusModified(StatusModified notification)
        {
            if (Accounts.SelectedItem is null) return;
            var (accountId, status) = notification;

            var account = Accounts.Items.FirstOrDefault(x => x.Id == accountId.Value);
            if (account is null) return;

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                account.Color = status.GetColor();
                SetPauseText(status);
            });
        }

        [ReactiveCommand(CanExecute = nameof(_canExecute))]
        private void AddAccount()
        {
            Accounts.SelectedItem = null;
            _accountTabStore.SetTabType(AccountTabType.AddAccount);
        }

        [ReactiveCommand(CanExecute = nameof(_canExecute))]
        private void AddAccounts()
        {
            Accounts.SelectedItem = null;
            _accountTabStore.SetTabType(AccountTabType.AddAccounts);
        }

        [ReactiveCommand(CanExecute = nameof(_canExecute))]
        private async Task DeleteAccount()
        {
            if (Accounts.SelectedItem is null)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "No account selected"));
                return;
            }
            if (Accounts.SelectedItem is null) return;

            var accountId = new AccountId(Accounts.SelectedItem.Id);
            using var scope = _serviceScopeFactory.CreateScope(accountId);

            var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();
            var status = taskManager.GetStatus(accountId);
            if (status != StatusEnums.Offline)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "Account should be offline"));
                return;
            }

            var result = await _dialogService.ConfirmBox.Handle(new MessageBoxData("Information", $"Are you sure want to delete \n {Accounts.SelectedItem.Content}"));
            if (!result) return;

            var deleteCommand = scope.ServiceProvider.GetRequiredService<DeleteCommand.Handler>();
            await deleteCommand.HandleAsync(new(accountId));
        }

        [ReactiveCommand(CanExecute = nameof(_canExecute))]
        private async Task Login()
        {
            if (Accounts.SelectedItem is null)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "No account selected"));
                return;
            }

            var accountId = new AccountId(Accounts.SelectedItem.Id);
            using var scope = _serviceScopeFactory.CreateScope(accountId);

            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
            var tribe = (TribeEnums)settingService.ByName(accountId, AccountSettingEnums.Tribe);
            if (tribe == TribeEnums.Any)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "Choose tribe first"));
                return;
            }

            var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();
            if (taskManager.GetStatus(accountId) != StatusEnums.Offline)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "Account should be offline"));
                return;
            }

            var getAccessQuery = scope.ServiceProvider.GetRequiredService<GetValidAccessCommand.Handler>();
            var result = await getAccessQuery.HandleAsync(new(accountId));
            if (result.IsFailed)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", result.ToString()));
                return;
            }

            var loginCommand = scope.ServiceProvider.GetRequiredService<LoginCommand.Handler>();

            await Observable.StartAsync(async () =>
            {
                await loginCommand.HandleAsync(new(accountId, result.Value));
            }, RxApp.TaskpoolScheduler);
        }

        [ReactiveCommand(CanExecute = nameof(_canExecute))]
        private async Task Logout()
        {
            if (Accounts.SelectedItem is null)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "No account selected"));
                return;
            }

            var accountId = new AccountId(Accounts.SelectedItem.Id);
            var status = _taskManager.GetStatus(accountId);
            switch (status)
            {
                case StatusEnums.Offline:
                    await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "Account's browser is already closed"));
                    return;

                case StatusEnums.Starting:
                case StatusEnums.Pausing:
                case StatusEnums.Stopping:
                    await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", $"TBS is {status}. Please waiting"));
                    return;

                case StatusEnums.Online:
                case StatusEnums.Paused:
                default:
                    break;
            }

            using var scope = _serviceScopeFactory.CreateScope(accountId);
            var logoutCommand = scope.ServiceProvider.GetRequiredService<LogoutCommand.Handler>();
            await Observable.StartAsync(async () =>
            {
                await logoutCommand.HandleAsync(new(accountId));
            }, RxApp.TaskpoolScheduler);
        }

        [ReactiveCommand(CanExecute = nameof(_canExecute))]
        private async Task Pause()
        {
            if (Accounts.SelectedItem is null)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "No account selected"));
                return;
            }

            var accountId = new AccountId(Accounts.SelectedItem.Id);

            var status = _taskManager.GetStatus(accountId);
            switch (status)
            {
                case StatusEnums.Paused:
                    _taskManager.SetStatus(accountId, StatusEnums.Online);
                    break;

                case StatusEnums.Online:
                    await Observable.StartAsync(async () =>
                    {
                        await _taskManager.StopCurrentTask(accountId);
                    }, RxApp.TaskpoolScheduler);

                    break;

                case StatusEnums.Offline:
                case StatusEnums.Starting:
                case StatusEnums.Pausing:
                case StatusEnums.Stopping:
                    await _dialogService.MessageBox.Handle(new MessageBoxData("Information", $"Account is {status}"));
                    break;

                default:
                    break;
            }
        }

        [ReactiveCommand(CanExecute = nameof(_canExecute))]
        private async Task Restart()
        {
            if (Accounts.SelectedItem is null)
            {
                await _dialogService.MessageBox.Handle(new MessageBoxData("Warning", "No account selected"));
                return;
            }

            var accountId = new AccountId(Accounts.SelectedItem.Id);
            var status = _taskManager.GetStatus(accountId);

            switch (status)
            {
                case StatusEnums.Offline:
                case StatusEnums.Starting:
                case StatusEnums.Pausing:
                case StatusEnums.Stopping:
                    await _dialogService.MessageBox.Handle(new MessageBoxData("Information", $"Account is {status}"));
                    return;

                case StatusEnums.Online:
                    await _dialogService.MessageBox.Handle(new MessageBoxData("Information", "Account should be paused first"));
                    return;

                case StatusEnums.Paused:
                    _taskManager.SetStatus(accountId, StatusEnums.Starting);
                    await Task.Delay(300);
                    _taskManager.Clear(accountId);
                    _rxQueue.Enqueue(new AccountInit(accountId));
                    _taskManager.SetStatus(accountId, StatusEnums.Online);
                    return;
            }
        }

        [ReactiveCommand]
        private StatusEnums GetStatus(AccountId accountId)
        {
            if (accountId == AccountId.Empty) return StatusEnums.Starting;
            return _taskManager.GetStatus(accountId);
        }

        [ReactiveCommand]
        private List<ListBoxItem> LoadAccount()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var taskManager = scope.ServiceProvider.GetRequiredService<ITaskManager>();
            var items = context.Accounts
                 .AsEnumerable()
                 .Select(x =>
                 {
                     var serverUrl = new Uri(x.Server);
                     var status = taskManager.GetStatus(new(x.Id));
                     return new ListBoxItem()
                     {
                         Id = x.Id,
                         Color = status.GetColor(),
                         Content = $"{x.Username}{Environment.NewLine}({serverUrl.Host})"
                     };
                 })
                 .ToList();
            return items;
        }

        [ReactiveCommand]
        private static string LoadVersion()
        {
            var versionAssembly = Assembly.GetExecutingAssembly().GetName().Version!;
            var version = new Version(versionAssembly.Major, versionAssembly.Minor, versionAssembly.Build);
            return $"{version}";
        }

        private void SetPauseText(StatusEnums status)
        {
            switch (status)
            {
                case StatusEnums.Offline:
                case StatusEnums.Starting:
                case StatusEnums.Pausing:
                case StatusEnums.Stopping:
                    PauseText = "[~~!~~]";
                    break;

                case StatusEnums.Online:
                    PauseText = "Pause";
                    break;

                case StatusEnums.Paused:
                    PauseText = "Resume";
                    break;

                default:
                    break;
            }


        }

        private void UpdateOnlineTime()
        {
            // Se nenhuma conta estiver selecionada na lista, zera a exibição
            if (Accounts.SelectedItem is null)
            {
                OnlineTimeText = "0.0 hours";
                OnlineTimeColor = "Black";
                return;
            }

            var accountId = Accounts.SelectedItem.Id;
            var accIdObj = new AccountId(accountId);
            var status = _taskManager.GetStatus(accIdObj);

            // Verifica qual é a tarefa rodando no momento
            var currentTask = _taskManager.GetCurrentTask(accIdObj);

            // Checa se a tarefa atual é o SleepTask
            bool isSleeping = currentTask is MainCore.Tasks.SleepTask.Task;

            // Só conta o tempo se o status for Online e NÃO estiver dormindo
            if (status == StatusEnums.Online && !isSleeping)
            {
                if (_lastUpdateTimes.TryGetValue(accountId, out var lastTime))
                {
                    var diff = DateTime.Now - lastTime;
                    if (!_accountOnlineTimes.ContainsKey(accountId))
                        _accountOnlineTimes[accountId] = TimeSpan.Zero;

                    _accountOnlineTimes[accountId] += diff;
                }
            }

            // Atualiza o relógio interno para a próxima checagem
            _lastUpdateTimes[accountId] = DateTime.Now;

            // Atualiza os textos e as cores que vão para a tela
            if (_accountOnlineTimes.TryGetValue(accountId, out var totalTime))
            {
                var hours = totalTime.TotalHours;
                OnlineTimeText = $"{hours:F1} hours"; // Exibe com 1 casa decimal (ex: 10.7)

                if (hours >= 10)
                    OnlineTimeColor = "Red";
                else if (hours >= 8)
                    OnlineTimeColor = "Orange";
                else
                    OnlineTimeColor = "Black";
            }
        }

        [ObservableAsProperty]
        private string _version = "";

        [Reactive]
        private string _pauseText = "[~~!~~]";
        [Reactive]
        private string _onlineTimeText = "0.0 hours";

        [Reactive]
        private string _onlineTimeColor = "Black"; // Cor padrão

        // Variáveis para controlar o tempo nos bastidores
        private readonly Dictionary<int, TimeSpan> _accountOnlineTimes = new();
        private readonly Dictionary<int, DateTime> _lastUpdateTimes = new();
    }
}
