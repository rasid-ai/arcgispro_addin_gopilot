using System.Text.Json;

namespace Rasid.Models
{
	internal class CatalogueItem
	{
		public int Index { get; set; }
		public string Label { get; set; }

		public static CatalogueItem FromJson(JsonElement props, int index)
		{
			string itemId = props.TryGetProperty("id", out var idEl) ? idEl.ToString() : $"Image {index + 1}";
			string date = props.TryGetProperty("datetime", out var dtEl) ? dtEl.GetString()
						: props.TryGetProperty("acquired", out var acEl) ? acEl.GetString()
						: null;
			string cloud = props.TryGetProperty("eo:cloud_cover", out var ccEl) ? ccEl.ToString()
						  : props.TryGetProperty("cloud_cover", out var cc2El) ? cc2El.ToString()
						  : null;

			var label = itemId;
			if (!string.IsNullOrEmpty(date)) label += $"  |  {date.Substring(0, System.Math.Min(10, date.Length))}";
			if (!string.IsNullOrEmpty(cloud)) label += $"  |  Cloud: {cloud}%";

			return new CatalogueItem { Index = index, Label = label };
		}
	}
}