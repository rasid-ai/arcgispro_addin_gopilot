using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rasid.Services
{
	internal static class LayerLoader
	{
		private static readonly HashSet<string> SupportedLayerExtensions =
			new(StringComparer.OrdinalIgnoreCase)
			{
				".tif", ".tiff", ".shp", ".geojson", ".gpkg"
			};

		public static bool LayerExists(string layerName)
		{
			if (QueuedTask.OnWorker)
			{
				var activeMap = MapView.Active?.Map;
				return activeMap?.GetLayersAsFlattenedList()
					.Any(layer => layer.Name == layerName) ?? false;
			}

			return QueuedTask.Run(() =>
			{
				var activeMap = MapView.Active?.Map;
				return activeMap?.GetLayersAsFlattenedList()
					.Any(layer => layer.Name == layerName) ?? false;
			}).Result;
		}

		public static async Task<Layer> LoadResultAsync(
			string filepath,
			string layerName,
			string groupName = "Solutions")
		{
			if (string.IsNullOrWhiteSpace(filepath) || !System.IO.File.Exists(filepath))
				throw new System.IO.FileNotFoundException(
					"The downloaded layer file was not found.", filepath);

			if (Path.GetExtension(filepath).Equals(
				".geojson",
				System.StringComparison.OrdinalIgnoreCase))
			{
				filepath = await ConvertGeoJsonAsync(filepath, layerName);
			}

			var mapView = MapView.Active;
			if (mapView == null)
			{
				throw new System.InvalidOperationException(
					"Open a map in ArcGIS Pro before loading this result.");
			}

			var layer = await QueuedTask.Run(() =>
			{
				var map = mapView.Map;

				var group = map.GetLayersAsFlattenedList()
					.OfType<GroupLayer>()
					.FirstOrDefault(layer => layer.Name == "RASID")
					?? LayerFactory.Instance.CreateGroupLayer(map, 0, "RASID");
				var subgroup = group.Layers
					.OfType<GroupLayer>()
					.FirstOrDefault(layer => layer.Name == groupName)
					?? LayerFactory.Instance.CreateGroupLayer(group, 0, groupName);

				var loadedLayer = LayerFactory.Instance.CreateLayer(
					new System.Uri(filepath),
					subgroup,
					layerName: layerName);

				if (loadedLayer != null)
					mapView.ZoomTo(loadedLayer);

				return loadedLayer;
			});

			if (layer == null)
			{
				throw new System.InvalidOperationException(
					$"ArcGIS Pro could not open '{Path.GetFileName(filepath)}' as a layer.");
			}

			return layer;
		}

		public static async Task<IReadOnlyList<Layer>> LoadResultsAsync(
			string filepath,
			string layerName,
			string groupName = "Solutions")
		{
			if (string.IsNullOrWhiteSpace(filepath) || !File.Exists(filepath))
				throw new FileNotFoundException(
					"The downloaded layer file was not found.", filepath);

			if (!IsZipArchive(filepath))
				return new[] { await LoadResultAsync(filepath, layerName, groupName) };

			var extractionRoot = ExtractArchive(filepath);
			var layerFiles = Directory
				.EnumerateFiles(extractionRoot, "*", SearchOption.AllDirectories)
				.Where(path => SupportedLayerExtensions.Contains(Path.GetExtension(path)))
				.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (layerFiles.Count == 0)
			{
				throw new InvalidDataException(
					"The ZIP file does not contain a supported Shapefile, GeoJSON, GeoPackage, or TIFF layer.");
			}

			var loadedLayers = new List<Layer>();
			for (var index = 0; index < layerFiles.Count; index++)
			{
				var extractedName = Path.GetFileNameWithoutExtension(layerFiles[index]);
				var displayName = layerFiles.Count == 1
					? layerName
					: $"{layerName} - {extractedName}";
				loadedLayers.Add(await LoadResultAsync(layerFiles[index], displayName, groupName));
			}

			return loadedLayers;
		}

		private static bool IsZipArchive(string filepath)
		{
			if (Path.GetExtension(filepath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
				return true;
			if (!File.Exists(filepath))
				return false;

			using var stream = File.OpenRead(filepath);
			if (stream.Length < 4)
				return false;

			var first = stream.ReadByte();
			var second = stream.ReadByte();
			var third = stream.ReadByte();
			var fourth = stream.ReadByte();
			return first == 0x50 && second == 0x4B &&
				((third == 0x03 && fourth == 0x04) ||
				 (third == 0x05 && fourth == 0x06) ||
				 (third == 0x07 && fourth == 0x08));
		}

		private static string ExtractArchive(string zipPath)
		{
			var extractionRoot = Path.Combine(
				Path.GetTempPath(),
				"rasid_downloads",
				$"{Path.GetFileNameWithoutExtension(zipPath)}_{Guid.NewGuid():N}");
			Directory.CreateDirectory(extractionRoot);

			var safeRoot = Path.GetFullPath(extractionRoot)
				.TrimEnd(Path.DirectorySeparatorChar) +
				Path.DirectorySeparatorChar;
			using var archive = ZipFile.OpenRead(zipPath);
			foreach (var entry in archive.Entries)
			{
				if (string.IsNullOrEmpty(entry.Name))
					continue;

				var destination = Path.GetFullPath(
					Path.Combine(extractionRoot, entry.FullName));
				if (!destination.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
					throw new InvalidDataException(
						"The ZIP file contains an unsafe path.");

				Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
				entry.ExtractToFile(destination, overwrite: true);
			}

			return extractionRoot;
		}

		private static async Task<string> ConvertGeoJsonAsync(
			string filepath,
			string layerName)
		{
			var geometryType = DetectArcGisGeometryType(filepath);
			var geodatabasePath = Project.Current?.DefaultGeodatabasePath;
			if (string.IsNullOrWhiteSpace(geodatabasePath))
				throw new System.InvalidOperationException(
					"The ArcGIS Pro project does not have a default geodatabase.");

			var safeName = Regex.Replace(
				layerName ?? "gopilot_result",
				@"[^A-Za-z0-9_]",
				"_");
			if (string.IsNullOrWhiteSpace(safeName))
				safeName = "gopilot_result";
			if (char.IsDigit(safeName[0]))
				safeName = "_" + safeName;

			var outputPath = Path.Combine(
				geodatabasePath,
				$"{safeName}_{System.DateTime.UtcNow:yyyyMMddHHmmssfff}");
			var result = await Geoprocessing.ExecuteToolAsync(
				"conversion.JSONToFeatures",
				Geoprocessing.MakeValueArray(
					filepath,
					outputPath,
					geometryType),
				null,
				null,
				GPExecuteToolFlags.None);

			var messages = string.Join(
				Environment.NewLine,
				result.Messages.Select(message =>
					$"[{message.Type}] {message.Text}"));
			if (!string.IsNullOrWhiteSpace(messages))
				Trace.WriteLine($"JSONToFeatures:{Environment.NewLine}{messages}");

			if (result.IsCanceled)
				throw new OperationCanceledException(
					"ArcGIS Pro canceled the GeoJSON conversion.");

			if (result.IsFailed)
				throw new System.InvalidOperationException(
					"ArcGIS Pro could not convert the GeoJSON file to a feature class." +
					(string.IsNullOrWhiteSpace(messages)
						? string.Empty
						: Environment.NewLine + messages));

			return outputPath;
		}

		private static string DetectArcGisGeometryType(string filepath)
		{
			using var stream = File.OpenRead(filepath);
			using var document = JsonDocument.Parse(stream);
			if (!document.RootElement.TryGetProperty("features", out var features) ||
				features.ValueKind != JsonValueKind.Array)
			{
				throw new InvalidDataException(
					"The GeoJSON does not contain a valid features array.");
			}

			var detectedTypes = new HashSet<string>(StringComparer.Ordinal);
			foreach (var feature in features.EnumerateArray())
			{
				if (!feature.TryGetProperty("geometry", out var geometry) ||
					geometry.ValueKind == JsonValueKind.Null)
				{
					continue;
				}

				if (!geometry.TryGetProperty("type", out var typeElement))
					throw new InvalidDataException(
						"A GeoJSON feature has no geometry type.");

				var geoJsonType = typeElement.GetString();
				var arcGisType = geoJsonType switch
				{
					"Point" => "POINT",
					"MultiPoint" => "MULTIPOINT",
					"LineString" or "MultiLineString" => "POLYLINE",
					"Polygon" or "MultiPolygon" => "POLYGON",
					_ => throw new NotSupportedException(
						$"Unsupported GeoJSON geometry type: {geoJsonType ?? "(missing)"}.")
				};
				detectedTypes.Add(arcGisType);
			}

			if (detectedTypes.Count == 0)
				throw new InvalidDataException(
					"The GeoJSON contains no supported geometries.");
			if (detectedTypes.Count > 1)
				throw new InvalidDataException(
					"The GeoJSON contains multiple geometry categories: " +
					string.Join(", ", detectedTypes) + ".");

			return detectedTypes.Single();
		}
	}
}
