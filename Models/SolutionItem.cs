using System.Text.Json.Serialization;

namespace Rasid.Models
{
	internal class SolutionItem
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("slug")]
		public string Slug { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("description_html")]
		public string DescriptionHtml { get; set; }

		[JsonPropertyName("image_url")]
		public string ImageUrl { get; set; }

		[JsonPropertyName("result_image_url")]
		public string ResultImageUrl { get; set; }

		[JsonPropertyName("status")]
		public string Status { get; set; }

		[JsonPropertyName("euro_per_km2")]
		public double EuroPerKm2 { get; set; }

		[JsonPropertyName("public")]
		public bool IsPublic { get; set; }

		[JsonPropertyName("results")]
		public bool HasResults { get; set; }

		[JsonPropertyName("zoom_level")]
		public int ZoomLevel { get; set; }
	}
}