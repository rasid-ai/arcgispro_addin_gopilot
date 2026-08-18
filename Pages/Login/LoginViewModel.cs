using ArcGIS.Desktop.Framework;
using Rasid.Commands;
using Rasid.Pages.Base;
using Rasid.Services;
using System;
using System.Diagnostics;
using System.Windows.Input;

namespace Rasid.Pages.Login
{
    internal class LoginViewModel : PageViewModelBase
    {
        private string _apiKey;
        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        private bool _isKeyVisible;
        public bool IsKeyVisible
        {
            get => _isKeyVisible;
            set => SetProperty(ref _isKeyVisible, value);
        }

        private string _errorText;
        public string ErrorText
        {
            get => _errorText;
            set => SetProperty(ref _errorText, value);
        }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set => SetProperty(ref _isConnecting, value);
        }

        public ICommand ToggleVisibilityCommand { get; }
        public ICommand GetApiKeyCommand { get; }
        public ICommand SignUpCommand { get; }
        public ICommand ConnectCommand { get; }

        public event Action LoggedIn;

        public LoginViewModel()
        {
            ToggleVisibilityCommand = new RelayCommand(() => IsKeyVisible = !IsKeyVisible);
            GetApiKeyCommand = new RelayCommand(() => OpenUrl(ApiClient.Instance.AppBaseUrl + "/api-keys"));
            SignUpCommand = new RelayCommand(() => OpenUrl(ApiClient.Instance.AppBaseUrl + "/auth?active_form=register"));
            ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        }

        private static void OpenUrl(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        private async System.Threading.Tasks.Task ConnectAsync()
        {
            if (IsConnecting)
                return;

            ErrorText = null;
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                ErrorText = "Enter your API key first.";
                return;
            }

            IsConnecting = true;
            try
            {
                AuthService.Instance.SetApiKey(ApiKey.Trim());
                var isAuthenticated = await AuthService.Instance.IsAuthenticatedAsync();
                if (!isAuthenticated)
                {
                    ErrorText = "Invalid API key. Please check it and try again.";
                    AuthService.Instance.ClearApiKey();
                    return;
                }

                AuthService.Instance.SaveApiKey(ApiKey.Trim());
                LoggedIn?.Invoke();
            }
            catch (Exception ex)
            {
                AuthService.Instance.ClearApiKey();
                ErrorText = $"Could not connect to RASID: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
            }
        }
    }
}
