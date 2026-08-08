using ArcGIS.Desktop.Framework;
using Rasid.Commands;
using Rasid.Models;
using Rasid.Pages.Base;
using Rasid.Pages.CreateProcessWizard;
using Rasid.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace Rasid.Pages.Processes
{
    internal class ProcessesViewModel : PageViewModelBase, IDisposable
    {
        private readonly RasidApiClient _api;
        private readonly ProjectItem _project;
        private readonly DispatcherTimer _refreshTimer;
        private int? _selectedProcessId;
        private bool _isDisposed;

        private bool _showEmptyState;
        public bool ShowEmptyState
        {
            get => _showEmptyState;
            private set => SetProperty(ref _showEmptyState, value);
        }

        public string ProjectTitle => _project.Title;
        public ObservableCollection<ProcessCardViewModel> Processes { get; } = new();

        private string _statusText = "Loading processes...";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private ProcessDetailViewModel _detail;
        public ProcessDetailViewModel Detail
        {
            get => _detail;
            set
            {
                if (ReferenceEquals(_detail, value))
                    return;

                _detail?.Dispose();
                SetProperty(ref _detail, value);
            }
        }

        private object _bodyContent;
        public object BodyContent
        {
            get => _bodyContent;
            set => SetProperty(ref _bodyContent, value);
        }

        private bool _isWizardActive;
        public bool IsWizardActive
        {
            get => _isWizardActive;
            set => SetProperty(ref _isWizardActive, value);
        }

        public ICommand NewProcessCommand { get; }
        public ICommand BackCommand { get; }

        public static readonly Dictionary<string, (string Label, string Color)> SituationLabels = new()
        {
            ["is"] = ("Completed", "#00856F"),
            ["done"] = ("Completed", "#00856F"),
            ["if"] = ("Failed", "#E74C3C"),
            ["failed"] = ("Failed", "#E74C3C"),
            ["pdmf"] = ("Failed", "#E74C3C"),
            ["i"] = ("Inference", "#3B82F6"),
            ["inference"] = ("Inference", "#3B82F6"),
            ["c"] = ("Preparation", "#6366F1"),
            ["creating"] = ("Preparation", "#6366F1"),
            ["idle"] = ("Preparation", "#6366F1")
        };

        public ProcessesViewModel(ProjectItem project)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _api = new RasidApiClient(ApiClient.Instance);
            NewProcessCommand = new RelayCommand(StartNewProcess);
            BackCommand = new RelayCommand(CloseWizard);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _refreshTimer.Tick += RefreshTimerOnTick;
            _ = LoadProcessesAsync();
        }

        private async void RefreshTimerOnTick(object sender, EventArgs e)
        {
            if (!_isDisposed && !IsWizardActive)
                await RefreshSilentlyAsync();
        }

        public async Task LoadProcessesAsync()
        {
            if (_isDisposed)
                return;

            ShowEmptyState = false;
            StatusText = "Loading processes...";
            try
            {
                var processes = await _api.GetProcessesAsync(_project.Slug);
                await PopulateListAsync(processes);

                var selected = Processes.FirstOrDefault(p => p.Process.Id == _selectedProcessId)
                               ?? Processes.FirstOrDefault();
                if (selected != null)
                    await SelectProcessAsync(selected);
                else
                    Detail = null;

                ShowEmptyState = Processes.Count == 0;
                StatusText = null;
                ManageAutoRefresh();
            }
            catch (Exception ex)
            {
                ShowEmptyState = false;
                StatusText = $"Failed to load processes: {ex.Message}";
                _refreshTimer.Stop();
            }
        }

        private async Task PopulateListAsync(IEnumerable<ProcessItem> processes)
        {
            Processes.Clear();
            foreach (var process in processes ?? Enumerable.Empty<ProcessItem>())
            {
                var capturedProcess = process;
                var (label, color) = SituationLabels.GetValueOrDefault(
                    capturedProcess.Situation ?? "idle", ("Preparation", "#6366F1"));

                var card = new ProcessCardViewModel
                {
                    Process = capturedProcess,
                    StatusLabel = label,
                    StatusColor = color
                };
                card.Thumbnail = await ImageLoader.LoadAsync(
                    capturedProcess.ThumbnailUrl,
                    ApiClient.Instance.ApiHost,
                    400,
                    220);
                card.SelectCommand = new AsyncRelayCommand(() => SelectProcessAsync(card));
                card.HideCommand = new AsyncRelayCommand(() => HideProcessAsync(capturedProcess));
                Processes.Add(card);
            }
        }

        private async Task SelectProcessAsync(ProcessCardViewModel card)
        {
            if (card == null || _isDisposed)
                return;

            foreach (var item in Processes)
                item.IsSelected = false;

            card.IsSelected = true;
            _selectedProcessId = card.Process.Id;
            StatusText = "Loading process details...";

            try
            {
                var detailJson = await _api.GetProcessDetailAsync(card.Process.Id);
                Detail = ProcessDetailViewModel.FromJson(detailJson, this, card.Thumbnail);
                StatusText = null;
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to load process details: {ex.Message}";
            }
        }

        private bool HasPreparingProcesses() =>
            Processes.Any(p => p.Process.Situation is "c" or "i" or "creating" or "inference" or "idle"
                                || !SituationLabels.ContainsKey(p.Process.Situation ?? string.Empty));

        private void ManageAutoRefresh()
        {
            if (!_isDisposed && !IsWizardActive && HasPreparingProcesses())
                _refreshTimer.Start();
            else
                _refreshTimer.Stop();
        }

        private async Task RefreshSilentlyAsync()
        {
            try
            {
                var processes = await _api.GetProcessesAsync(_project.Slug);
                await PopulateListAsync(processes);
                var selected = Processes.FirstOrDefault(p => p.Process.Id == _selectedProcessId)
                               ?? Processes.FirstOrDefault();
                if (selected != null)
                    await SelectProcessAsync(selected);
                else
                    Detail = null;
                ShowEmptyState = Processes.Count == 0;
                ManageAutoRefresh();
            }
            catch
            {
                
            }
        }

        private async Task HideProcessAsync(ProcessItem process)
        {
            var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                $"Are you sure you want to hide '{process.Name}'?",
                "Hide Process",
                System.Windows.MessageBoxButton.YesNo);
            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            await _api.HideProcessAsync(_project.Slug, process.Id);
            await LoadProcessesAsync();
        }

        private void StartNewProcess()
        {
            CloseWizardContent();
            _refreshTimer.Stop();

            var wizardVm = new CreateProcessWizardViewModel(_project.Slug, _project.Title);
            wizardVm.ProcessCreated += _ => CloseWizard();
            wizardVm.Cancelled += CloseWizard;
            BodyContent = wizardVm;
            IsWizardActive = true;
            StatusText = null;
        }

        private void CloseWizard()
        {
            IsWizardActive = false;
            CloseWizardContent();
            _ = LoadProcessesAsync();
        }

        private void CloseWizardContent()
        {
            if (BodyContent is IDisposable disposable)
                disposable.Dispose();

            BodyContent = null;
        }

        public void Dispose()
        {
            _isDisposed = true;
            _refreshTimer.Stop();
            _refreshTimer.Tick -= RefreshTimerOnTick;
            Detail = null;
            CloseWizardContent();
        }
    }
}
