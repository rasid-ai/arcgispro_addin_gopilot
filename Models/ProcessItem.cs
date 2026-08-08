using System.Text.Json.Serialization;

namespace Rasid.Models
{
	internal class ProcessItem
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("situation")]
		public string Situation { get; set; }

		[JsonPropertyName("thumbnail")]
		public string ThumbnailUrl { get; set; }
	}
}
