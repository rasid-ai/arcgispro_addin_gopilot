using ArcGIS.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using Rasid.Commands;
using Rasid.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Rasid.Pages.Processes
{
    internal class ProcessDetailViewModel : PropertyChangedBase, IDisposable
    {
        private readonly SubscriptionToken _layersRemovedSubscription;
        private HashSet<Layer> _resultLayers = new();
        private HashSet<Layer> _datasetLayers = new();
        private string _resultLayerName;
        private string _datasetLayerName;

        private ProcessDetailViewModel()
        {
            _layersRemovedSubscription = LayersRemovedEvent.Subscribe(
                OnLayersRemoved);
        }

        public string Name { get; set; }
        public string StatusLabel { get; set; }
        public string StatusColor { get; set; }
        public Dictionary<string, string> GeneralInfo { get; set; } = new();
        public Dictionary<string, string> Analytics { get; set; } = new();
        public bool HasAnalytics => Analytics.Count > 0;
        public bool IsCompleted { get; set; }
        public string NotCompletedMessage { get; set; }
        public bool ShowDownloads { get; set; }
        public BitmapImage Thumbnail { get; set; }

        public string ResultVectorUrl { get; set; }
        public string DatasetUrl { get; set; }

        private string _resultVectorButtonText = "Download Result (Shapefile)";
        public string ResultVectorButtonText
        {
            get => _resultVectorButtonText;
            set => SetProperty(ref _resultVectorButtonText, value);
        }

        private string _datasetButtonText = "Download Dataset (GeoTIFF)";
        public string DatasetButtonText
        {
            get => _datasetButtonText;
            set => SetProperty(ref _datasetButtonText, value);
        }

        private ICommand _downloadResultCommand;
        public ICommand DownloadResultCommand
        {
            get => _downloadResultCommand;
            set => SetProperty(ref _downloadResultCommand, value);
        }

        private ICommand _downloadDatasetCommand;
        public ICommand DownloadDatasetCommand
        {
            get => _downloadDatasetCommand;
            set => SetProperty(ref _downloadDatasetCommand, value);
        }

        public static ProcessDetailViewModel FromJson(
            JsonElement process,
            ProcessesViewModel owner,
            BitmapImage thumbnail = null)
        {
            var situation = process.TryGetProperty("situation", out var situationElement)
                ? situationElement.GetString()
                : "idle";
            var (label, color) = ProcessesViewModel.SituationLabels.GetValueOrDefault(
                situation ?? "idle", ("Preparation", "#6366F1"));

            var vm = new ProcessDetailViewModel
            {
                Name = process.TryGetProperty("name", out var name) ? name.GetString() : "Process",
                StatusLabel = label,
                StatusColor = color,
                IsCompleted = situation is "is" or "done",
                Thumbnail = thumbnail
            };

            vm.GeneralInfo["Fees"] = process.TryGetProperty("fees", out var fees) ? $"€{fees}" : "-";
            vm.GeneralInfo["Date"] = process.TryGetProperty("create_date", out var date) ? date.ToString() : "-";
            vm.GeneralInfo["Area"] = process.TryGetProperty("area", out var area) ? $"{area} km²" : "-";

            if (process.TryGetProperty("analytics", out var analytics) && analytics.ValueKind == JsonValueKind.Object)
                foreach (var property in analytics.EnumerateObject())
                    vm.Analytics[property.Name] = property.Value.ToString();

            var resultVector = process.TryGetProperty("result_file_shp", out var resultElement)
                ? resultElement.GetString()
                : null;
            var dataset = process.TryGetProperty("dataset", out var datasetElement)
                ? datasetElement.GetString()
                : null;
            var processName = vm.Name ?? "process";

            if (vm.IsCompleted && (!string.IsNullOrWhiteSpace(resultVector) || !string.IsNullOrWhiteSpace(dataset)))
            {
                vm.ShowDownloads = true;
                vm.ResultVectorUrl = resultVector;
                vm.DatasetUrl = dataset;

                var resultLayerName = $"{processName} (result)";
                var datasetLayerName = $"{processName} (dataset)";
                vm._resultLayerName = resultLayerName;
                vm._datasetLayerName = datasetLayerName;
                if (LayerLoader.LayerExists(resultLayerName))
                    vm.ResultVectorButtonText = "✓ Already Loaded";
                if (LayerLoader.LayerExists(datasetLayerName))
                    vm.DatasetButtonText = "✓ Already Loaded";

                if (!string.IsNullOrWhiteSpace(resultVector))
                    vm.DownloadResultCommand = new AsyncRelayCommand(() =>
                        DownloadAndLoadAsync(resultVector, resultLayerName, vm, isDataset: false));
                if (!string.IsNullOrWhiteSpace(dataset))
                    vm.DownloadDatasetCommand = new AsyncRelayCommand(() =>
                        DownloadAndLoadAsync(dataset, datasetLayerName, vm, isDataset: true));
            }
            else if (!vm.IsCompleted)
            {
                vm.NotCompletedMessage = $"Process is not yet completed ({label}).";
            }

            return vm;
        }

        private static async System.Threading.Tasks.Task DownloadAndLoadAsync(
            string url,
            string layerName,
            ProcessDetailViewModel vm,
            bool isDataset)
        {
            var api = new RasidApiClient(ApiClient.Instance);
            try
            {
                var path = await api.DownloadFileAsync(url);
                var layers = await LayerLoader.LoadResultsAsync(path, layerName);
                if (isDataset)
                    vm._datasetLayers = layers.ToHashSet();
                else
                    vm._resultLayers = layers.ToHashSet();

                if (isDataset)
                    vm.DatasetButtonText = "✓ Loaded!";
                else
                    vm.ResultVectorButtonText = "✓ Loaded!";
            }
            catch (System.Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"Failed to download:\n{ex.Message}",
                    "Download Error");
            }
        }

        private void OnLayersRemoved(LayerEventsArgs args)
        {
            var removedLayers = args.Layers.ToList();
            var removedNames = removedLayers
                .Select(layer => layer.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var solutionsGroupRemoved = removedNames.Contains("GoBox");
            var resultRemoved = solutionsGroupRemoved ||
                _resultLayers.Overlaps(removedLayers) ||
                (!string.IsNullOrWhiteSpace(_resultLayerName) &&
                 removedNames.Contains(_resultLayerName));
            var datasetRemoved = solutionsGroupRemoved ||
                _datasetLayers.Overlaps(removedLayers) ||
                (!string.IsNullOrWhiteSpace(_datasetLayerName) &&
                 removedNames.Contains(_datasetLayerName));

            if (!resultRemoved && !datasetRemoved)
                return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (resultRemoved && !string.IsNullOrWhiteSpace(ResultVectorUrl))
                    {
                        _resultLayers.Clear();
                        ResultVectorButtonText = "Download Result (Shapefile)";
                        DownloadResultCommand = new AsyncRelayCommand(() =>
                            DownloadAndLoadAsync(
                                ResultVectorUrl,
                                _resultLayerName,
                                this,
                                isDataset: false));
                    }

                    if (datasetRemoved && !string.IsNullOrWhiteSpace(DatasetUrl))
                    {
                        _datasetLayers.Clear();
                        DatasetButtonText = "Download Dataset (GeoTIFF)";
                        DownloadDatasetCommand = new AsyncRelayCommand(() =>
                            DownloadAndLoadAsync(
                                DatasetUrl,
                                _datasetLayerName,
                                this,
                                isDataset: true));
                    }
                }));
        }

        public void Dispose()
        {
            LayersRemovedEvent.Unsubscribe(_layersRemovedSubscription);
        }
    }
}
