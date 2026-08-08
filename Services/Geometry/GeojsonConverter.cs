using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace Rasid.Services.Geometry
{
    internal static class GeoJsonConverter
    {
        public static string FromPolygon(Polygon wgs84Polygon)
        {
            if (wgs84Polygon == null)
                throw new ArgumentNullException(nameof(wgs84Polygon));

            return JsonSerializer.Serialize(new
            {
                type = "Polygon",
                coordinates = GetClosedRings(wgs84Polygon)
            });
        }

        public static Task<string> FromLayerAsync(FeatureLayer layer)
        {
            if (layer == null)
                throw new ArgumentNullException(nameof(layer));

            return QueuedTask.Run(() =>
            {
                using var featureClass = layer.GetFeatureClass();
                var wgs84 = SpatialReferences.WGS84;
                var features = new List<object>();

                using var cursor = featureClass.Search();
                while (cursor.MoveNext())
                {
                    using var feature = cursor.Current as Feature;
                    if (feature?.GetShape() is not Polygon sourcePolygon)
                        continue;

                    var projected = GeometryEngine.Instance.Project(sourcePolygon, wgs84) as Polygon;
                    if (projected == null || projected.IsEmpty)
                        continue;

                    features.Add(new
                    {
                        type = "Feature",
                        geometry = new
                        {
                            type = "Polygon",
                            coordinates = GetClosedRings(projected)
                        },
                        properties = new { }
                    });
                }

                if (features.Count == 0)
                    throw new InvalidOperationException(
                        "The selected layer does not contain polygon features.");

                return JsonSerializer.Serialize(new
                {
                    type = "FeatureCollection",
                    features
                });
            });
        }

        private static double[][][] GetClosedRings(Polygon polygon)
        {
            return polygon.Parts.Select(part =>
            {
                var points = part
                    .Select(segment => new[] { segment.StartPoint.X, segment.StartPoint.Y })
                    .ToList();

                if (points.Count > 0 && !SameCoordinate(points[0], points[^1]))
                    points.Add(new[] { points[0][0], points[0][1] });

                return points.ToArray();
            }).ToArray();
        }

        private static bool SameCoordinate(double[] left, double[] right)
        {
            const double tolerance = 1e-12;
            return Math.Abs(left[0] - right[0]) <= tolerance &&
                   Math.Abs(left[1] - right[1]) <= tolerance;
        }
    }
}
