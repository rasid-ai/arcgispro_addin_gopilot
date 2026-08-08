using System;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Mapping;
using Rasid.Pages.Shared;
using System.Windows.Input;

namespace Rasid.Services.Geometry
{
    internal sealed class GeometryInputManager : IDisposable
    {
        public event Action<GeoJsonResult> GeometryReady;
        public event Action DrawStarted;
        public event Action DrawCancelled;

        private DrawingToolbarWindow _toolbarWindow;
        private DrawingToolbarViewModel _toolbarViewModel;
        private bool _isDrawing;
        private bool _disposed;// Flag to indicate if the object has been disposed

        public async Task<bool> StartDrawModeAsync(string helpContext = "general")
        {
            ThrowIfDisposed();

            if (_isDrawing)
                CancelDrawMode();

            if (DrawingHelpDialogView.ShouldShow() &&
                !await DrawingHelpDialogView.ShowHelpAsync(helpContext))
            {
                return false;
            }

            if (MapView.Active == null)
            {
                MessageBox.Show(
                    "Open or activate a map before drawing an area.",
                    "RASID — No active map");
                return false;
            }

            SubscribeToSketchTool(); 

            _toolbarViewModel = new DrawingToolbarViewModel();
            _toolbarViewModel.CancelRequested += CancelDrawMode;
            _toolbarViewModel.UndoRequested += UndoLastPoint;
            _toolbarViewModel.ClearRequested += ClearSketch;
            _toolbarViewModel.FinishRequested += FinishSketch;

            try
            {
                _isDrawing = true;

                var mapView = MapView.Active;
                foreach (var paneObject in FrameworkApplication.Panes)
                {
                    if (paneObject is Pane pane &&
                        paneObject is IMapPane mapPane &&
                        mapPane.MapView == mapView)
                    {
                        pane.Activate();
                        break;
                    }
                }

                await FrameworkApplicationHelpers.ActivateToolAsync(AoiSketchTool.ToolId);
                FrameworkApplication.Current.MainWindow.AddHandler(
                    Keyboard.PreviewKeyDownEvent,
                    new KeyEventHandler(OnDrawingKeyDown),
                    true);

                _toolbarWindow = new DrawingToolbarWindow
                {
                    DataContext = _toolbarViewModel,
                    Owner = FrameworkApplication.Current.MainWindow
                };
                _toolbarWindow.Show();

                DrawStarted?.Invoke();
                return true;
            }
            catch
            {
                StopDrawMode(restoreExploreTool: false);
                throw;
            }
        }

        private void SubscribeToSketchTool()
        {
            UnsubscribeFromSketchTool();//prevent multiple subscriptions
            //subscribe to the events of the AoiSketchTool class
            AoiSketchTool.PolygonFinished += OnPolygonFinished;
            AoiSketchTool.MeasurementsUpdated += OnMeasurementsUpdated;
            AoiSketchTool.ToolDeactivated += OnToolDeactivated;
        }

        private void UnsubscribeFromSketchTool()
        {
            //unsubscribe from the events of the AoiSketchTool class to avoid memory leaks and unintended behavior
            AoiSketchTool.PolygonFinished -= OnPolygonFinished;
            AoiSketchTool.MeasurementsUpdated -= OnMeasurementsUpdated;
            AoiSketchTool.ToolDeactivated -= OnToolDeactivated;
        }

        private void OnMeasurementsUpdated(
            int pointCount,
            double area,
            double perimeter)
        {
            _toolbarViewModel?.UpdateInfo(pointCount, area, perimeter);
        }

        private void OnPolygonFinished(Polygon wgs84Polygon)
        {
            if (!_isDrawing || wgs84Polygon == null)
                return;

            var geojson = GeoJsonConverter.FromPolygon(wgs84Polygon);
            StopDrawMode(restoreExploreTool: true);

            GeometryReady?.Invoke(new GeoJsonResult
            {
                GeoJson = geojson,
                Source = "draw",
                Name = "Drawn Polygon"
            });
        }

        private void OnToolDeactivated()
        {
            if (!_isDrawing)
                return;

            // The user intentionally selected another ArcGIS Pro tool, so do
            // not force the Explore tool and overwrite their selection.
            StopDrawMode(restoreExploreTool: false);
            DrawCancelled?.Invoke();
        }

        public void CancelDrawMode()
        {
            if (!_isDrawing)
                return;

            StopDrawMode(restoreExploreTool: true);
            DrawCancelled?.Invoke();
        }

        private void StopDrawMode(bool restoreExploreTool)
        {
            _isDrawing = false;
            FrameworkApplication.Current.MainWindow.RemoveHandler(
                Keyboard.PreviewKeyDownEvent,
                new KeyEventHandler(OnDrawingKeyDown));
            UnsubscribeFromSketchTool();

            if (_toolbarViewModel != null)
            {
                _toolbarViewModel.CancelRequested -= CancelDrawMode;
                _toolbarViewModel.UndoRequested -= UndoLastPoint;
                _toolbarViewModel.ClearRequested -= ClearSketch;
                _toolbarViewModel.FinishRequested -= FinishSketch;
            }

            _toolbarViewModel = null;

            if (_toolbarWindow != null)
            {
                _toolbarWindow.Close();
                _toolbarWindow = null;
            }

            if (restoreExploreTool)
                _ = FrameworkApplicationHelpers.DeactivateToExploreAsync();
        }

        private static void UndoLastPoint() =>
            _ = AoiSketchTool.UndoCurrentSketchAsync();

        private static void ClearSketch() =>
            _ = AoiSketchTool.ClearCurrentSketchAsync();

        private static void FinishSketch() =>
            _ = AoiSketchTool.FinishCurrentSketchAsync();

        private void OnDrawingKeyDown(object sender, KeyEventArgs args)
        {
            if (!_isDrawing)
                return;

            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                CancelDrawMode();
            }
            else if (args.Key == Key.Back || args.Key == Key.Delete)
            {
                args.Handled = true;
                UndoLastPoint();
            }
            else if (args.Key == Key.Enter || args.Key == Key.Return)
            {
                args.Handled = true;
                FinishSketch();
            }
        }

        public async Task<bool> ShowLayerPickerAsync(string title = "Select Layer")
        {
            ThrowIfDisposed();

            var dialog = new LayerPickerDialog
            {
                Title = title,
                Owner = FrameworkApplication.Current.MainWindow
            };
            var vm = new LayerPickerViewModel();
            dialog.DataContext = vm;
            await vm.LoadLayersAsync();

            if (dialog.ShowDialog() == true && vm.SelectedLayer != null)
            {
                var geojson = await GeoJsonConverter.FromLayerAsync(vm.SelectedLayer);
                GeometryReady?.Invoke(new GeoJsonResult
                {
                    GeoJson = geojson,
                    Source = "layer",
                    Name = vm.SelectedLayer.Name
                });
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_isDrawing)
                StopDrawMode(restoreExploreTool: true);
            else
                UnsubscribeFromSketchTool();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GeometryInputManager));
        }
    }

    internal sealed class GeoJsonResult
    {
        public string GeoJson { get; set; }
        public string Source { get; set; }
        public string Name { get; set; }
    }
}
