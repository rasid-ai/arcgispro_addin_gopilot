using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Mapping;
using Rasid.Commands;
using Rasid.Models;
using Rasid.Pages.Base;
using Rasid.Services;
using Rasid.Services.Geometry;

namespace Rasid.Pages.CreateProcessWizard
{
	//main logic for Create Process form
	internal class CreateProcessWizardViewModel : PageViewModelBase, IDisposable
	{
		private readonly RasidApiClient _api;
		private readonly GeometryInputManager _geoManager;
		private readonly string _projectSlug;
		private ProcessConfig _config;
		private List<JsonElement> _catalogueResultsRaw;

		//maps application names to short server configuration name
		private static readonly Dictionary<string, string> TypeAbbrev = new()
		{
			["mapbox"] = "mapb",
			["raster_link"] = "lras",
			["raster_upload"] = "uras",
			["sentinel_catalogue"] = "sent",
			["sentinel_series"] = "sts",
			["planet"] = "plan"
		};

		public string ProjectTitle { get; }
		public ObservableCollection<DatasetTypeOption> DatasetTypeOptions { get; } = new();
		public ObservableCollection<CatalogueItem> CatalogueResults { get; } = new();

		private string _name;
		public string Name { get => _name; set => SetProperty(ref _name, value); } //process name

		private string _datasetType;
		public string DatasetType
		{
			get => _datasetType;
			set { SetProperty(ref _datasetType, value); UpdateSectionVisibility(); }
		}
		//visibility of form sections based on dataset type
		public bool IsAoiVisible => DatasetType is "mapbox" or "sentinel_catalogue" or "sentinel_series" or "planet";
		public bool IsDateVisible => DatasetType is "sentinel_catalogue" or "sentinel_series" or "planet";
		public bool IsSentinelVisible => DatasetType == "sentinel_catalogue";
		public bool IsCatalogueVisible => DatasetType is "sentinel_catalogue" or "planet";
		public bool IsZoomVisible => DatasetType == "mapbox";
		public bool IsUploadVisible => DatasetType == "raster_upload";
		public bool IsLinkVisible => DatasetType == "raster_link";
		public bool IsEndDateVisible => !(_config?.Sentinel?.Fields.GetValueOrDefault("end_date")?.Hidden ?? false);


		private void UpdateSectionVisibility()
		{
			foreach (var p in new[] { nameof(IsAoiVisible), nameof(IsDateVisible), nameof(IsSentinelVisible),
				nameof(IsCatalogueVisible), nameof(IsZoomVisible), nameof(IsUploadVisible),
				nameof(IsLinkVisible), nameof(IsEndDateVisible) })
				NotifyPropertyChanged(p);
		}

		//AOI state
		private string _aoiCoordsJson; // list of [lon, lat]
		private string _aoiDisplay = "No AOI set. Draw on map or pick from layer.";
		public string AoiDisplay { get => _aoiDisplay; set => SetProperty(ref _aoiDisplay, value); }

		// date/sentinel/zoom/upload/link fields
		private DateTime _startDate = DateTime.Today.AddMonths(-1);
		public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

		private DateTime _endDate = DateTime.Today;
		public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }

		public ObservableCollection<ChoiceOption> DataCollectionOptions { get; } = new();
		public ObservableCollection<ChoiceOption> EvalScriptOptions { get; } = new();
		public ObservableCollection<ChoiceOption> SortByOptions { get; } = new();

		private ChoiceOption _selectedDataCollection;
		public ChoiceOption SelectedDataCollection { get => _selectedDataCollection; set => SetProperty(ref _selectedDataCollection, value); }

		private ChoiceOption _selectedEvalScript;
		public ChoiceOption SelectedEvalScript { get => _selectedEvalScript; set => SetProperty(ref _selectedEvalScript, value); }

		private ChoiceOption _selectedSortBy;
		public ChoiceOption SelectedSortBy { get => _selectedSortBy; set => SetProperty(ref _selectedSortBy, value); }

		private int _zoomLevel = 18;
		public int ZoomLevel { get => _zoomLevel; set => SetProperty(ref _zoomLevel, value); }

		private string _uploadFileName = "No file selected";
		public string UploadFileName { get => _uploadFileName; set => SetProperty(ref _uploadFileName, value); }
		private string _uploadPath;

		private string _rasterLink;
		public string RasterLink { get => _rasterLink; set => SetProperty(ref _rasterLink, value); }

		private bool _isBusy;
		public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

		private string _loadingText = "Loading process configuration...";
		public string LoadingText { get => _loadingText; set => SetProperty(ref _loadingText, value); }

		public bool IsFormReady => _config != null;

		public ICommand DrawAoiCommand { get; }
		public ICommand PickLayerCommand { get; }
		public ICommand SearchCatalogueCommand { get; }
		public ICommand ChooseFileCommand { get; }
		public ICommand SubmitCommand { get; }
		public ICommand CancelCommand { get; }

		public event Action<ProcessItem> ProcessCreated;
		public event Action Cancelled;

		public CreateProcessWizardViewModel(string projectSlug, string projectTitle)
		{
			_api = new RasidApiClient(ApiClient.Instance);
			_geoManager = new GeometryInputManager();
			_geoManager.GeometryReady += OnGeometryReady;
			_projectSlug = projectSlug;
			ProjectTitle = projectTitle;

			DrawAoiCommand = new AsyncRelayCommand(
				() => _geoManager.StartDrawModeAsync("wizard"));
			PickLayerCommand = new AsyncRelayCommand(PickAoiFromLayerAsync);
			SearchCatalogueCommand = new AsyncRelayCommand(SearchCatalogueAsync);
			ChooseFileCommand = new RelayCommand(ChooseFile);
			SubmitCommand = new AsyncRelayCommand(SubmitAsync);
			CancelCommand = new RelayCommand(() => Cancelled?.Invoke());

			_ = LoadConfigAsync();
		}

		private async Task LoadConfigAsync()
		{
			try
			{
				var json = await _api.GetProcessConfigAsync(_projectSlug);
				_config = json.Deserialize<ProcessConfig>();
				if (_config == null)
					throw new InvalidOperationException("The process configuration response was empty.");
				BuildDatasetTypeOptions();
				PopulateSentinelChoices();
				ZoomLevel = ReadIntDefault(_config.Defaults, "zoom_level", 18);
				NotifyPropertyChanged(nameof(IsFormReady));
			}
			catch (Exception ex)
			{
				LoadingText = $"Failed to load config: {ex.Message}";
			}
		}
		private static int ReadIntDefault(Dictionary<string, object> values, string key, int fallback)
		{
			if (values == null || !values.TryGetValue(key, out var raw) || raw == null)
				return fallback;

			if (raw is JsonElement json)
			{
				if (json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var number))
					return number;
				if (json.ValueKind == JsonValueKind.String && int.TryParse(json.GetString(), out number))
					return number;
				return fallback;
			}

			return int.TryParse(raw.ToString(), out var parsed) ? parsed : fallback;
		}

		private void BuildDatasetTypeOptions()
		{
			var labels = new (string key, string label)[]
			{
				//internal key label and visible label for each dataset type
				("mapbox", "Mapbox Tiles"), ("sentinel_catalogue", "Sentinel Catalogue"),
				("sentinel_series", "Sentinel Time Series"), ("planet", "Planet Imagery"),
				("raster_upload", "Upload Raster"), ("raster_link", "Raster Link")
			};
			DatasetTypeOptions.Clear();
			var supportedTypes = _config.Supports ?? new Dictionary<string, bool>();
			foreach (var (key, label) in labels)
				if (supportedTypes.GetValueOrDefault(key))
					DatasetTypeOptions.Add(new DatasetTypeOption { Key = key, Label = label });

			if (DatasetTypeOptions.Count > 0)
				DatasetType = DatasetTypeOptions[0].Key;
		}

		private void PopulateSentinelChoices()
		{
			var sentinel = _config.Sentinel ?? new SentinelConfig();
			var choices = sentinel.Choices ?? new Dictionary<string, JsonElement>();

			//helper to parse choice options from JsonElement
			List<ChoiceOption> ParseChoices(string key)
			{
				var result = new List<ChoiceOption>();
				if (!choices.TryGetValue(key, out var element))
					return result;

				try
				{
					//Try to parse as array of choice options
					if (element.ValueKind == JsonValueKind.Array)
					{
						foreach (var item in element.EnumerateArray())
						{
							var choice = JsonSerializer.Deserialize<ChoiceOption>(item.GetRawText());
							if (choice != null)
								result.Add(choice);
						}
					}
					//Handle nested dictionary structure 
					else if (element.ValueKind == JsonValueKind.Object)
					{
						foreach (var prop in element.EnumerateObject())
						{
							//For nested structures, flatten into choices
							if (prop.Value.ValueKind == JsonValueKind.Array)
							{
								foreach (var item in prop.Value.EnumerateArray())
								{
									var choice = JsonSerializer.Deserialize<ChoiceOption>(item.GetRawText());
									if (choice != null)
										result.Add(choice);
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[CONFIG DEBUG] Error parsing {key}: {ex.Message}");
				}

				return result;
			}

			foreach (var ch in ParseChoices("data_collection")) DataCollectionOptions.Add(ch);
			foreach (var ch in ParseChoices("eval_script")) EvalScriptOptions.Add(ch);
			foreach (var ch in ParseChoices("eval_scripts_by_collection")) EvalScriptOptions.Add(ch);
			foreach (var ch in ParseChoices("sort_by")) SortByOptions.Add(ch);

			SelectedDataCollection = DataCollectionOptions.FirstOrDefault();
			SelectedEvalScript = EvalScriptOptions.FirstOrDefault();
			SelectedSortBy = SortByOptions.FirstOrDefault();

			var defaults = sentinel.Defaults ?? new Dictionary<string, string>();
			if (defaults.TryGetValue("start_date", out var sd) && DateTime.TryParse(sd, out var parsedStart))
				StartDate = parsedStart;
			if (defaults.TryGetValue("end_date", out var ed) && DateTime.TryParse(ed, out var parsedEnd))
				EndDate = parsedEnd;
		}

		private void OnGeometryReady(GeoJsonResult result)
		{
			using var doc = JsonDocument.Parse(result.GeoJson);
			var root = doc.RootElement;
			List<double[]> coords = new();

			if (root.GetProperty("type").GetString() == "Polygon")
			{
				foreach (var pt in root.GetProperty("coordinates")[0].EnumerateArray())
					coords.Add(new[] { pt[0].GetDouble(), pt[1].GetDouble() });
			}
			else if (root.GetProperty("type").GetString() == "FeatureCollection")
			{
				var features = root.GetProperty("features");
				if (features.GetArrayLength() > 0)
				{
					var geom = features[0].GetProperty("geometry");
					var ring = geom.GetProperty("type").GetString() == "MultiPolygon"
						? geom.GetProperty("coordinates")[0][0]
						: geom.GetProperty("coordinates")[0];
					foreach (var pt in ring.EnumerateArray())
						coords.Add(new[] { pt[0].GetDouble(), pt[1].GetDouble() });
				}
			}

			_aoiCoordsJson = JsonSerializer.Serialize(coords);
			AoiDisplay = _aoiCoordsJson;
		}

		private async Task PickAoiFromLayerAsync()
		{
			await _geoManager.ShowLayerPickerAsync("Select AOI Layer");

		}

		private async Task SearchCatalogueAsync()
		{
			if (_aoiCoordsJson == null)
			{
				Notify("AOI Required", "Draw or select an AOI first.");
				return;
			}
			var coords = JsonSerializer.Deserialize<List<double[]>>(_aoiCoordsJson);
			var lons = coords.Select(c => c[0]).ToList();
			var lats = coords.Select(c => c[1]).ToList();
			var bbox = new List<double> { lons.Min(), lats.Min(), lons.Max(), lats.Max() };

			var isPlanet = DatasetType == "planet";
			var payload = new Dictionary<string, object>
			{
				["bbox"] = bbox
			};
			if (isPlanet)
			{
				payload["planet_start_date"] = StartDate.ToString("yyyy-MM-dd");
				payload["planet_end_date"] = EndDate.ToString("yyyy-MM-dd");
			}
			else
			{
				payload["sentinel_start_date"] = StartDate.ToString("yyyy-MM-dd");
				payload["sentinel_end_date"] = EndDate.ToString("yyyy-MM-dd");
				payload["sentinel_data_collection"] = SelectedDataCollection?.Value;
				payload["sentinel_sort_by"] = SelectedSortBy?.Value;
			}

			try
			{
				var results = await _api.SearchCatalogueAsync(payload, isPlanet);
				CatalogueResults.Clear();
				_catalogueResultsRaw = new List<JsonElement>();
				var items = results.ValueKind == JsonValueKind.Object &&
					results.TryGetProperty("features", out var f) ? f
					: results.ValueKind == JsonValueKind.Object &&
					  results.TryGetProperty("results", out var r) ? r
					: results;
				if (items.ValueKind != JsonValueKind.Array)
					throw new InvalidOperationException(
						"The catalogue response did not contain an imagery list.");
				foreach (var item in items.EnumerateArray())
				{
					_catalogueResultsRaw.Add(item);
					var props = item.TryGetProperty("properties", out var p) ? p : item;
					CatalogueResults.Add(CatalogueItem.FromJson(props, _catalogueResultsRaw.Count - 1));
				}
			}
			catch (Exception ex)
			{
				Notify("Search Failed", ex.Message);
			}
		}

		private CatalogueItem _selectedCatalogueItem;
		public CatalogueItem SelectedCatalogueItem
		{
			get => _selectedCatalogueItem;
			set => SetProperty(ref _selectedCatalogueItem, value);
		}

		private void ChooseFile()
		{
			var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "TIFF Files (*.tif;*.tiff)|*.tif;*.tiff" };
			if (dialog.ShowDialog() == true)
			{
				_uploadPath = dialog.FileName;
				UploadFileName = System.IO.Path.GetFileName(dialog.FileName);
			}
		}

		private async Task SubmitAsync()
		{
			if (IsBusy) return;//prevents duplicate submission
			if (string.IsNullOrWhiteSpace(Name)) { Notify("Missing Name", "Enter a process name."); return; }
			if (string.IsNullOrEmpty(DatasetType)) { Notify("Missing Type", "Select a dataset type."); return; }

			var payload = new Dictionary<string, object> { ["name"] = Name, ["zoom_level"] = ZoomLevel };

			var abbrev = TypeAbbrev.GetValueOrDefault(DatasetType, DatasetType);
			var datasetTypeIds = _config.DatasetTypeToId ?? new Dictionary<string, int>();
			if (datasetTypeIds.TryGetValue(abbrev, out var choiceId) ||
				datasetTypeIds.TryGetValue(DatasetType, out choiceId))
				payload["dataset_choice_id"] = choiceId;

			Dictionary<string, string> files = null;

			switch (DatasetType)
			{
				case "mapbox":
					if (_aoiCoordsJson == null) { Notify("Missing AOI", "Draw or select an AOI."); return; }
					payload["mapbox_aoi"] = _aoiCoordsJson;
					if (IsZoomVisible) payload["zoom_level"] = ZoomLevel;
					break;

				case "sentinel_catalogue":
					if (_aoiCoordsJson == null) { Notify("Missing AOI", "Draw or select an AOI."); return; }
					payload["sentinel_aoi"] = _aoiCoordsJson;
					payload["sentinel_data_collection"] = SelectedDataCollection?.Value;
					payload["sentinel_start_date"] = StartDate.ToString("yyyy-MM-dd");
					payload["sentinel_end_date"] = EndDate.ToString("yyyy-MM-dd");
					payload["sentinel_sort_by"] = SelectedSortBy?.Value;

					var evalFixed = _config.Sentinel.Fields.GetValueOrDefault("eval_script")?.FixedValue;
					payload["sentinel_eval_script"] = !string.IsNullOrEmpty(evalFixed) ? evalFixed : SelectedEvalScript?.Value;

					if (SelectedCatalogueItem != null && _catalogueResultsRaw != null)
						payload["sentinel_search_results"] = _catalogueResultsRaw[SelectedCatalogueItem.Index].GetRawText();
					break;

				case "sentinel_series":
					if (_aoiCoordsJson == null) { Notify("Missing AOI", "Draw or select an AOI."); return; }
					var sFields = _config.Sentinel.Fields;
					var start = StartDate.ToString("yyyy-MM-dd");
					var end = (sFields.GetValueOrDefault("end_date")?.Hidden ?? false) ? start : EndDate.ToString("yyyy-MM-dd");
					payload["sentinel_aoi"] = _aoiCoordsJson;
					payload["sentinel_start_date"] = start;
					payload["sentinel_end_date"] = end;
					payload["sentinel_data_collection"] = sFields.GetValueOrDefault("data_collection")?.FixedValue ?? "sentinel-2-l1c";
					payload["sentinel_eval_script"] = sFields.GetValueOrDefault("eval_script")?.FixedValue ?? "s2-l1c-all-bands";
					payload["sentinel_series_interval_range"] = 7;
					payload["sentinel_series_gap_between_intervals"] = 0;
					break;

				case "planet":
					if (_aoiCoordsJson == null) { Notify("Missing AOI", "Draw or select an AOI."); return; }
					payload["planet_aoi"] = _aoiCoordsJson;
					payload["planet_start_date"] = StartDate.ToString("yyyy-MM-dd");
					payload["planet_end_date"] = EndDate.ToString("yyyy-MM-dd");
					if (SelectedCatalogueItem != null && _catalogueResultsRaw != null)
						payload["planet_search_results"] = _catalogueResultsRaw[SelectedCatalogueItem.Index].GetRawText();
					break;

				case "raster_upload":
					if (_uploadPath == null) { Notify("Missing File", "Choose a TIFF file to upload."); return; }
					files = new Dictionary<string, string> { ["upload_raster"] = _uploadPath };
					break;

				case "raster_link":
					if (string.IsNullOrWhiteSpace(RasterLink)) { Notify("Missing Link", "Enter a raster URL."); return; }
					payload["link_to_raster"] = RasterLink;
					break;
			}

			IsBusy = true;
			try
			{
				var result = await _api.CreateProcessAsync(_projectSlug, payload, files);
				Notify("Process Created", "Process created successfully!");
				ProcessCreated?.Invoke(result);
			}
			catch (Exception ex)
			{
				Notify("Error", $"Failed to create process:\n{ex.Message}");
			}
			finally { IsBusy = false; }
		}

		private void Notify(string title, string message) =>
			ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(message, title);

		public void Dispose()
		{
			_geoManager.GeometryReady -= OnGeometryReady;
			_geoManager.Dispose();
		}
	}

	internal class DatasetTypeOption { public string Key { get; set; } public string Label { get; set; } }
}
