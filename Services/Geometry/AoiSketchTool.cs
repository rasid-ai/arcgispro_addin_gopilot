using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace Rasid.Services.Geometry
{
	internal class AoiSketchTool : MapTool
	{
		internal const string ToolId = "Rasid_AoiSketchTool";

		internal static event Action<int, double, double> MeasurementsUpdated;
		internal static event Action<Polygon> PolygonFinished;
		internal static event Action ToolDeactivated;

		private static IDisposable _aoiOverlay;
		private static AoiSketchTool _activeTool;

		private readonly List<MapPoint> _vertices = new();
		private readonly List<IDisposable> _previewOverlays = new();
		private bool _sketchHandled;

		protected override Task OnToolActivateAsync(bool hasMapViewChanged)
		{
			_activeTool = this;
			_sketchHandled = false;
			_vertices.Clear();
			MeasurementsUpdated?.Invoke(0, 0, 0);
			return Task.CompletedTask;
		}

		protected override void OnToolMouseDown(
			MapViewMouseButtonEventArgs args)
		{
			if (args.ChangedButton == MouseButton.Left ||
				args.ChangedButton == MouseButton.Right)
			{
				args.Handled = true;
			}

			base.OnToolMouseDown(args);
		}

		protected override async Task HandleMouseDownAsync(
			MapViewMouseButtonEventArgs args)
		{
			if (args.ChangedButton == MouseButton.Left)
			{
				var mapView = MapView.Active;
				if (mapView == null)
					return;

				var point = await QueuedTask.Run(() =>
					mapView.ClientToMap(args.ClientPoint));
				if (point == null)
					return;

				_vertices.Add(point);
				await RenderPreviewAsync();
			}
			else if (args.ChangedButton == MouseButton.Right)
			{
				await FinishVerticesAsync();
			}
		}

		private async Task RenderPreviewAsync()
		{
			var points = _vertices.ToList();
			var measurements = await QueuedTask.Run(() =>
			{
				ClearPreviewOverlays();
				var mapView = MapView.Active;
				if (mapView == null || points.Count == 0)
					return (0d, 0d);

				var blue = ColorFactory.Instance.CreateRGBColor(30, 136, 229);
				var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(
					blue,
					7,
					SimpleMarkerStyle.Circle);

				foreach (var point in points)
				{
					_previewOverlays.Add(mapView.AddOverlay(
						point,
						pointSymbol.MakeSymbolReference()));
				}

				double area = 0;
				double perimeter = 0;

				if (points.Count >= 2)
				{
					var line = PolylineBuilderEx.CreatePolyline(
						points,
						points[0].SpatialReference);
					var lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(
						blue,
						2.5,
						SimpleLineStyle.Solid);
					_previewOverlays.Add(mapView.AddOverlay(
						line,
						lineSymbol.MakeSymbolReference()));
					perimeter = GeometryEngine.Instance.GeodesicLength(
						line,
						LinearUnit.Meters);
				}

				if (points.Count >= 3)
				{
					var polygon = PolygonBuilderEx.CreatePolygon(
						points,
						points[0].SpatialReference);
					var outline = SymbolFactory.Instance.ConstructStroke(
						blue,
						2.5,
						SimpleLineStyle.Solid);
					var polygonSymbol = SymbolFactory.Instance
						.ConstructPolygonSymbol(
							ColorFactory.Instance.CreateRGBColor(
								30,
								136,
								229,
								45),
							SimpleFillStyle.Solid,
							outline);
					_previewOverlays.Add(mapView.AddOverlay(
						polygon,
						polygonSymbol.MakeSymbolReference()));
					area = Math.Abs(GeometryEngine.Instance.GeodesicArea(
						polygon,
						AreaUnit.SquareMeters));
				}

				return (area, perimeter);
			});

			MeasurementsUpdated?.Invoke(
				points.Count,
				measurements.Item1,
				measurements.Item2);
		}

		private async Task<bool> FinishVerticesAsync()
		{
			if (_vertices.Count < 3)
				return false;

			var points = _vertices.ToList();
			var result = await QueuedTask.Run(() =>
			{
				var polygon = PolygonBuilderEx.CreatePolygon(
					points,
					points[0].SpatialReference);
				var projected = GeometryEngine.Instance.Project(
					polygon,
					SpatialReferences.WGS84) as Polygon;

				var mapView = MapView.Active;
				if (mapView != null)
				{
					_aoiOverlay?.Dispose();
					var outline = SymbolFactory.Instance.ConstructStroke(
						ColorFactory.Instance.GreenRGB,
						2.5,
						SimpleLineStyle.Solid);
					var symbol = SymbolFactory.Instance.ConstructPolygonSymbol(
						null,
						outline);
					_aoiOverlay = mapView.AddOverlay(
						polygon,
						symbol.MakeSymbolReference());
				}

				ClearPreviewOverlays();
				return projected;
			});

			if (result == null)
				return false;

			_sketchHandled = true;
			PolygonFinished?.Invoke(result);
			return true;
		}

		private async Task UndoLastVertexAsync()
		{
			if (_vertices.Count == 0)
				return;

			_vertices.RemoveAt(_vertices.Count - 1);
			await RenderPreviewAsync();
		}

		private async Task ClearVerticesAsync()
		{
			_vertices.Clear();
			await RenderPreviewAsync();
		}

		internal static Task UndoCurrentSketchAsync() =>
			_activeTool?.UndoLastVertexAsync() ?? Task.CompletedTask;

		internal static Task ClearCurrentSketchAsync() =>
			_activeTool?.ClearVerticesAsync() ?? Task.CompletedTask;

		internal static Task<bool> FinishCurrentSketchAsync() =>
			_activeTool?.FinishVerticesAsync() ?? Task.FromResult(false);

		public static void ClearAoiOverlay()
		{
			_aoiOverlay?.Dispose();
			_aoiOverlay = null;
		}

		private void ClearPreviewOverlays()
		{
			foreach (var overlay in _previewOverlays)
				overlay.Dispose();
			_previewOverlays.Clear();
		}

		protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
		{
			if (ReferenceEquals(_activeTool, this))
				_activeTool = null;

			ClearPreviewOverlays();
			_vertices.Clear();

			if (!_sketchHandled)
			{
				_sketchHandled = true;
				ToolDeactivated?.Invoke();
			}

			return Task.CompletedTask;
		}
	}
}
