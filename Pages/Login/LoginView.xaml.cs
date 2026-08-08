using System.Windows;
using System.Windows.Controls;

namespace Rasid.Pages.Login
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private void ApiKeyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel && sender is PasswordBox passwordBox)
                viewModel.ApiKey = passwordBox.Password;
        }
    }
}
