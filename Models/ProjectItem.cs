using System;
using System.Text.Json.Serialization;

namespace Rasid.Models
{
	internal class ProjectItem
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("slug")]
		public string Slug { get; set; }

		[JsonPropertyName("title")]
		public string Title { get; set; }

		[JsonPropertyName("thumbnail")]
		public string ThumbnailUrl { get; set; }

		[JsonPropertyName("processes_number")]
		public int ProcessesNumber { get; set; }

		[JsonPropertyName("system_modification_date")]
		public DateTimeOffset? SystemModificationDate { get; set; }

		[JsonPropertyName("solution")]
		public SolutionItem Solution { get; set; }
	}
}
