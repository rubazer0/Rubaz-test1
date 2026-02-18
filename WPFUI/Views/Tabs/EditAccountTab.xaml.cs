using MainCore.UI.ViewModels.Tabs;
using ReactiveUI;
using System.Reactive.Disposables.Fluent;

namespace WPFUI.Views.Tabs
{
    // ESTA CLASSE É FUNDAMENTAL PARA O XAML FUNCIONAR
    public class EditAccountTabBase : ReactiveUserControl<EditAccountViewModel>
    {
    }

    /// <summary>
    /// Interaction logic for EditAccountTab.xaml
    /// </summary>
    public partial class EditAccountTab : EditAccountTabBase
    {
        public EditAccountTab()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                // Botões de Acesso
                this.BindCommand(ViewModel, vm => vm.AddAccessCommand, v => v.AddAccessButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.EditAccessCommand, v => v.EditAccessButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.DeleteAccessCommand, v => v.DeleteAccessButton).DisposeWith(d);

                // Botão de Salvar Conta
                this.BindCommand(ViewModel, vm => vm.EditAccountCommand, v => v.EditAccountButton).DisposeWith(d);

                // Botão de Testar Telegram
                this.BindCommand(ViewModel, vm => vm.TestTelegramCommand, v => v.TestTelegramButton).DisposeWith(d);

                // Campos da Conta
                this.Bind(ViewModel, vm => vm.AccountInput.Username, v => v.NicknameTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccountInput.Server, v => v.ServerTextBox.Text).DisposeWith(d);

                // Grid de Proxies
                this.OneWayBind(ViewModel, vm => vm.AccountInput.Accesses, v => v.ProxiesDataGrid.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.SelectedAccess, v => v.ProxiesDataGrid.SelectedItem).DisposeWith(d);

                // Campos do Proxy (AccessInput)
                this.Bind(ViewModel, vm => vm.AccessInput.Username, v => v.UsernameTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccessInput.Password, v => v.PasswordTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccessInput.ProxyHost, v => v.ProxyHostTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccessInput.ProxyPort, v => v.ProxyPortTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccessInput.ProxyUsername, v => v.ProxyUsernameTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccessInput.ProxyPassword, v => v.ProxyPasswordTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.AccessInput.Useragent, v => v.UseragentTextBox.Text).DisposeWith(d);

                // Campos do Telegram
                this.Bind(ViewModel, vm => vm.TelegramToken, v => v.TelegramTokenText.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.TelegramChatId, v => v.TelegramChatIdText.Text).DisposeWith(d);
            });
        }
    }
}
