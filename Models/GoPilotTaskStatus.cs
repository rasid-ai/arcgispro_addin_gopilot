using System.Text.Json.Serialization;

namespace Rasid.Models
{
	internal class GoPilotTaskStatus
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("task_id")]
		public string TaskId { get; set; }

		[JsonPropertyName("status")]
		public string Status { get; set; }

		[JsonPropertyName("result")]
		public string ResultText { get; set; }

		[JsonPropertyName("llm_message")]
		public LlmMessage LlmMessage { get; set; }

		[JsonPropertyName("progress")]
		public double Progress { get; set; }

		[JsonPropertyName("llm_generating")]
		public bool LlmGenerating { get; set; }

		[JsonPropertyName("files_processing")]
		public bool FilesProcessing { get; set; }

		[JsonPropertyName("files_ready")]
		public bool FilesReady { get; set; }
	}

	internal class LlmMessage
	{
		[JsonPropertyName("content")]
		public string Content { get; set; }
	}
}
