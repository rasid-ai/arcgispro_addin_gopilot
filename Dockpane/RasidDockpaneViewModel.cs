using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Rasid.Pages.About;
using Rasid.Pages.Base;
using Rasid.Pages.GoPilot;
using Rasid.Pages.Login;
using Rasid.Pages.Processes;
using Rasid.Pages.Projects;
using Rasid.Pages.Shared;
using Rasid.Pages.Solutions;
using Rasid.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Rasid.Dockpane
{
    internal class RasidDockpaneViewModel : DockPane
    {
        //id declared in config.daml 
        private const string DockPaneId = "Rasid_DockpaneID";
        private const string ProfileUrl = "https://app.rasid.ai/profile";
        private const string CreditsUrl = "https://app.rasid.ai/payment/refill";

        public static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
            if (pane == null)
                throw new InvalidOperationException($"DockPane '{DockPaneId}' could not be found. Check Config.daml.");

            pane.Activate();
        }

        private PageViewModelBase _currentPage;
        public PageViewModelBase CurrentPage
        {
            get => _currentPage;
            set
            {
                if (ReferenceEquals(_currentPage, value))
                    return;

                if (_currentPage is IDisposable disposable)
                    disposable.Dispose();

                SetProperty(ref _currentPage, value);
            }
        }

        private string _currentSection;
        public string CurrentSection
        {
            get => _currentSection;
            set => SetProperty(ref _currentSection, value);
        }

        private bool _isNavigationVisible;
        public bool IsNavigationVisible
        {
            get => _isNavigationVisible;
            set => SetProperty(ref _isNavigationVisible, value);
        }

        private string _welcomeText = "Welcome to RASID";
        public string WelcomeText
        {
            get => _welcomeText;
            set => SetProperty(ref _welcomeText, value);
        }

        private string _creditsText = string.Empty;
        public string CreditsText
        {
            get => _creditsText;
            set => SetProperty(ref _creditsText, value);
        }


        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public ICommand ShowGoPilotCommand { get; }
        public ICommand ShowSolutionsCommand { get; }
        public ICommand ShowProjectsCommand { get; }
        public ICommand ShowFeedbackCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public ICommand LogOutCommand { get; }
        public ICommand OpenProfileCommand { get; }
        public ICommand OpenCreditsCommand { get; }

        public RasidDockpaneViewModel()
        {
            ShowGoPilotCommand = new RelayCommand(ShowGoPilot);
            ShowSolutionsCommand = new RelayCommand(ShowSolutions);
            ShowProjectsCommand = new RelayCommand(ShowProjects);
            ShowFeedbackCommand = new RelayCommand(ShowFeedback);
            ShowAboutCommand = new RelayCommand(() => Navigate("About", new AboutViewModel()));
            LogOutCommand = new RelayCommand(LogOut);
            OpenProfileCommand = new RelayCommand(() => OpenExternalUrl(ProfileUrl));
            OpenCreditsCommand = new RelayCommand(() => OpenExternalUrl(CreditsUrl));

            Initialize();
        }

        private static void OpenExternalUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private async void Initialize()
        {
            try
            {
                if (!AuthService.Instance.LoadApiKey())
                {
                    ShowLogin();
                    return;
                }

                if (!await AuthService.Instance.IsAuthenticatedAsync())
                {
                    AuthService.Instance.ClearCredentials();
                    ShowLogin();
                    return;
                }

                await CompleteLoginAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RASID initialization failed: {ex}");
                ShowLogin();
            }
        }

        private void Navigate(string section, PageViewModelBase page)
        {
            IsNavigationVisible = true;
            CurrentSection = section;
            CurrentPage = page;
        }

        private void ShowGoPilot() => Navigate("GoPilot", new GoPilotViewModel());

        private void ShowSolutions()
        {
            var vm = new SolutionsViewModel();
            vm.ProjectCreated += project => Navigate("Projects", new ProcessesViewModel(project));
            Navigate("Solutions", vm);
        }

        private void ShowProjects()
        {
            var vm = new ProjectsViewModel();
            vm.ProjectOpened += project => Navigate("Projects", new ProcessesViewModel(project));
            Navigate("Projects", vm);
        }

        private void ShowFeedback()
        {
            var vm = new FeedbackDialogViewModel(CurrentSection);
            var dialog = new FeedbackDialogView
            {
                DataContext = vm,
                Owner = FrameworkApplication.Current.MainWindow
            };
            vm.Submitted += () => dialog.DialogResult = true;
            dialog.ShowDialog();
        }

        private void ShowLogin()
        {
            IsNavigationVisible = false;
            CurrentSection = "Login";
            WelcomeText = "Welcome to RASID";
            CreditsText = string.Empty;

            var loginVm = new LoginViewModel();
            loginVm.LoggedIn += async () => await CompleteLoginAsync();
            CurrentPage = loginVm;
        }

        private async Task CompleteLoginAsync()
        {
            try
            {
                var profile = await AuthService.Instance.GetProfileAsync();


                if (profile == null)
                {
                    WelcomeText = "Welcome to RASID";
                    CreditsText = string.Empty;
                }
                else
                {
                    WelcomeText = string.IsNullOrWhiteSpace(profile.Name)
                        ? "Welcome to RASID"
                        : $"Welcome, {profile.Name}";
                    CreditsText = $"Credits: €{profile.Credits:0.##}";
                }
            }
            catch (Exception)
            {
                WelcomeText = "Welcome to RASID";
                CreditsText = string.Empty;
            }

            ShowGoPilot();
        }

        private void LogOut()
        {
            AuthService.Instance.ClearCredentials();
            ShowLogin();
        }
    }
}
