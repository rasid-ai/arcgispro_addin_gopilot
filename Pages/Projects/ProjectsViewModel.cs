using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Rasid.Commands;
using Rasid.Models;
using Rasid.Pages.Base;
using Rasid.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Rasid.Pages.Projects
{
    internal class ProjectsViewModel : PageViewModelBase
    {
        private readonly RasidApiClient _api;

        public ObservableCollection<ProjectCardViewModel> Projects { get; } = new();
        public event Action<ProjectItem> ProjectOpened;

        private string _statusText = "Loading projects...";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public ProjectsViewModel()
        {
            _api = new RasidApiClient(ApiClient.Instance);
            _ = LoadProjectsAsync();
        }

        private async Task LoadProjectsAsync()
        {
            StatusText = "Loading projects...";
            try
            {
                var projects = await _api.GetUserProjectsAsync();
                Projects.Clear();
                foreach (var project in projects)
                {
                    var capturedProject = project;
                    var card = new ProjectCardViewModel
                    {
                        Project = capturedProject,
                        OpenCommand = new RelayCommand(() => ProjectOpened?.Invoke(capturedProject)),
                        HideCommand = new AsyncRelayCommand(() => HideProjectAsync(capturedProject))
                    };

                    card.Thumbnail = await ImageLoader.LoadAsync(
                        project.ThumbnailUrl,
                        ApiClient.Instance.ApiHost,
                        600,
                        360);

                    Projects.Add(card);
                }

                StatusText = Projects.Count == 0 ? "No projects found. Create one from Solutions." : null;
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to load projects: {ex.Message}";
            }
        }

        private async Task HideProjectAsync(ProjectItem project)
        {
            var dialog = new HideProjectDialog(project.Title)
            {
                Owner = FrameworkApplication.Current.MainWindow
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                StatusText = $"Hiding {project.Title}...";
                await _api.HideProjectAsync(project.Slug);
                await LoadProjectsAsync();
            }
            catch (Exception ex)
            {
                StatusText = null;
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"Failed to hide project:\n\n{ex.Message}",
                    "Hide Project Failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    internal class ProjectCardViewModel : PropertyChangedBase
    {
        public ProjectItem Project { get; set; }
        public ICommand OpenCommand { get; set; }
        public ICommand HideCommand { get; set; }

        private BitmapImage _thumbnail;
        public BitmapImage Thumbnail
        {
            get => _thumbnail;
            set => SetProperty(ref _thumbnail, value);
        }

        public string ModifiedDateText =>
            Project?.SystemModificationDate?.ToLocalTime().ToString("MM/dd/yyyy") ?? string.Empty;

        public string ProcessesText =>
            $"Processes: {Project?.ProcessesNumber ?? 0}";
    }
}
