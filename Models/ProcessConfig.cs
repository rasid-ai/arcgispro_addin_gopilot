using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rasid.Models
{
	internal class ProcessConfig
	{
		[JsonPropertyName("supports")] public Dictionary<string, bool> Supports { get; set; } = new();
		[JsonPropertyName("fields")] public Dictionary<string, object> Fields { get; set; } = new();
		[JsonPropertyName("sentinel")] public SentinelConfig Sentinel { get; set; } = new();
		[JsonPropertyName("defaults")] public Dictionary<string, object> Defaults { get; set; } = new();
		[JsonPropertyName("dataset_type_to_id")] public Dictionary<string, int> DatasetTypeToId { get; set; } = new();
	}

	internal class SentinelConfig
	{
		[JsonPropertyName("fields")] public Dictionary<string, SentinelFieldConfig> Fields { get; set; } = new();
		[JsonPropertyName("choices")] public Dictionary<string, JsonElement> Choices { get; set; } = new();
		[JsonPropertyName("defaults")] public Dictionary<string, string> Defaults { get; set; } = new();
	}

	internal class SentinelFieldConfig
	{
		[JsonPropertyName("label")] public string Label { get; set; }
		[JsonPropertyName("hidden")] public bool Hidden { get; set; }
		[JsonPropertyName("fixed_value")] public string FixedValue { get; set; }
	}

	internal class ChoiceOption
	{
		[JsonPropertyName("label")] public string Label { get; set; }
		[JsonPropertyName("value")] public string Value { get; set; }
	}
}