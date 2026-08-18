using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using Rasid.Commands;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace Rasid.Pages.Shared
{
    internal class LayerPickerViewModel : PropertyChangedBase
    {
        public ObservableCollection<FeatureLayer> Layers { get; } = new();

        private FeatureLayer _selectedLayer;
        public FeatureLayer SelectedLayer
        {
            get => _selectedLayer;
            set
            {
                SetProperty(ref _selectedLayer, value);
                NotifyPropertyChanged(nameof(HasSelection));
            }
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool HasSelection => SelectedLayer != null;
        public ICommand RefreshCommand { get; }

        public LayerPickerViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(LoadLayersAsync);
        }

        public async Task LoadLayersAsync()
        {
            StatusText = "Loading map layers...";

            // Read ArcGIS map state on the MCT, then update the WPF collection
            // after returning to the UI thread.
            var layers = await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                return map == null
                    ? new List<FeatureLayer>()
                    : map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
            });

            Layers.Clear();
            foreach (var layer in layers)
                Layers.Add(layer);

            SelectedLayer = Layers.FirstOrDefault();
            StatusText = Layers.Count == 0
                ? "No feature layers are available in the active map."
                : "Select a polygon feature layer.";
        }
    }
}
