using MainCore.UI.ViewModels.Tabs;
using ReactiveUI;
using System.Reactive.Disposables.Fluent;

namespace WPFUI.Views.Tabs
{
    public class AccountSettingTabBase : ReactiveUserControl<AccountSettingViewModel>
    {
    }

    /// <summary>
    /// Interaction logic for AccountSettingTab.xaml
    /// </summary>
    public partial class AccountSettingTab : AccountSettingTabBase
    {
        public AccountSettingTab()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                // Comandos
                this.BindCommand(ViewModel, vm => vm.ExportCommand, v => v.ExportButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.ImportCommand, v => v.ImportButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.SaveCommand, v => v.SaveButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.TestTelegramCommand, v => v.TestTelegramButton).DisposeWith(d);

                // --- LIGAÇÃO DOS CAMPOS DE TEXTO DO TELEGRAM ---
                this.Bind(ViewModel, vm => vm.TelegramToken, v => v.TelegramTokenText.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.TelegramChatId, v => v.TelegramChatIdText.Text).DisposeWith(d);
                // -----------------------------------------------

                // Outros Bindings
                this.Bind(ViewModel, vm => vm.AccountSettingInput.ClickDelay, v => v.ClickDelay.ViewModel).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.TaskDelay, v => v.TaskDelay.ViewModel).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.WorkTime, v => v.WorkTime.ViewModel).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.SleepTime, v => v.SleepTime.ViewModel).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.EnableAutoLoadVillage, v => v.EnableAutoLoadVillage.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.Tribe, v => v.Tribes.ViewModel).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.HeadlessChrome, v => v.HeadlessChrome.IsChecked).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountSettingInput.EnableAutoStartAdventure, v => v.EnableAutoStartAdventure.IsChecked).DisposeWith(d);
            });
        }
    }
}
