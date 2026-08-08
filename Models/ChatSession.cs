using System;
using System.Text.Json.Serialization;

namespace Rasid.Models
{
	internal class ChatSession
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("title")]
		public string Title { get; set; }

		[JsonPropertyName("system_registration_date")]
		public DateTime SystemRegistrationDate { get; set; }

		[JsonPropertyName("system_modification_date")]
		public DateTime SystemModificationDate { get; set; }

		[JsonPropertyName("message_count")]
		public int MessageCount { get; set; }

		public DateTime CreatedAt
		{
			get => SystemRegistrationDate;
			set => SystemRegistrationDate = value;
		}
	}
}
