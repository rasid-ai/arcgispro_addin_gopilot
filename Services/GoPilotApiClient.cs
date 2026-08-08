using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ArcGISEventLog = ArcGIS.Desktop.Framework.Utilities.EventLog;
using Rasid.Models;

namespace Rasid.Services
{
	internal class GoPilotApiClient
	{
		private readonly ApiClient _api;
		public GoPilotApiClient(ApiClient api) { _api = api; }

		
		public Task<JsonElement> CreateSessionAsync(string content,
			object inputMetadata = null, List<string> filePaths = null,
			string geoJsonData = null) =>
			PostMessageAsync("sessions/", "create_session", content,
				inputMetadata, filePaths, geoJsonData);
		
		public Task<JsonElement> GetSessionHistoryAsync() => GetAt<JsonElement>("sessions/history/");

		
		public Task<JsonElement> GetSessionAsync(int sessionId) =>
			GetAt<JsonElement>($"sessions/{sessionId}/");

		
		public async Task DeleteSessionAsync(int sessionId)
		{
			using var response = await _api.Raw.DeleteAsync(
				_api.GoPilotBaseUrl + $"sessions/{sessionId}/");
			response.EnsureSuccessStatusCode();
		}

		
		public async Task<JsonElement> SendMessageAsync(int sessionId, string content,
			object inputMetadata = null, List<string> filePaths = null,
			string geoJsonData = null) =>
			await PostMessageAsync($"sessions/{sessionId}/send_message/", "send_message",
				content, inputMetadata, filePaths, geoJsonData);

		private async Task<JsonElement> PostMessageAsync(string path, string operation,
			string content, object inputMetadata, List<string> filePaths,
			string geoJsonData)
		{
			var stopwatch = Stopwatch.StartNew();
            ArcGISEventLog.Write(ArcGISEventLog.EventType.Information,
				$"[GoPilot] {operation} start: path={path}, " +
				$"contentLength={content?.Length ?? 0}, files={filePaths?.Count ?? 0}, " +
				$"hasGeoJson={!string.IsNullOrWhiteSpace(geoJsonData)}", flush: true);
			using var form = new MultipartFormDataContent { { new StringContent(content), "content" } };
			form.Add(new StringContent(JsonSerializer.Serialize(
				inputMetadata ?? new { type = "text" })), "input_metadata");
			if (!string.IsNullOrWhiteSpace(geoJsonData))
				form.Add(new StringContent(geoJsonData), "geojson_data");

			if (filePaths != null)
			{
				foreach (var filePath in filePaths)
				{
					var bytes = await File.ReadAllBytesAsync(filePath);
					form.Add(new ByteArrayContent(bytes), "files", Path.GetFileName(filePath));
				}
			}

			try
			{
				using var response = await _api.Raw.PostAsync(
					_api.GoPilotBaseUrl + path, form);
				var rawJson = await response.Content.ReadAsStringAsync();
                ArcGISEventLog.Write(ArcGISEventLog.EventType.Information,
					$"[GoPilot] {operation} response: status={(int)response.StatusCode} " +
					$"({response.StatusCode}), elapsedMs={stopwatch.ElapsedMilliseconds}", flush: true);
				if (!response.IsSuccessStatusCode)
                    ArcGISEventLog.Write(ArcGISEventLog.EventType.Error,
						$"[GoPilot] {operation} error body: {rawJson}", flush: true);

				response.EnsureSuccessStatusCode();
				return JsonSerializer.Deserialize<JsonElement>(rawJson);
			}
			catch (System.Exception ex)
			{
                ArcGISEventLog.Write(ArcGISEventLog.EventType.Error,
					$"[GoPilot] {operation} failed after {stopwatch.ElapsedMilliseconds} ms:\n{ex}",
					flush: true);
				throw;
			}
		}

		
		public Task<List<ChatMessage>> GetMessagesAsync(int sessionId) =>
			GetAt<List<ChatMessage>>($"sessions/{sessionId}/messages/");

		
		public Task<GoPilotTaskStatus> GetTaskStatusAsync(string taskId) =>
			GetAt<GoPilotTaskStatus>($"sessions/tasks/{taskId}/");

		
		public Task<JsonElement> GetUserFilesAsync() => GetAt<JsonElement>("files/user_files/");

		
		public Task<JsonElement> GetFileAsync(int fileId) => GetAt<JsonElement>($"files/{fileId}/");

		
		public Task<JsonElement> DeleteFileAsync(int fileId) =>
			PostAt<JsonElement>($"files/{fileId}/delete_file/", null, isDelete: true);

		
		public Task<JsonElement> CheckFileProcessingStatusAsync(int fileId) =>
			GetAt<JsonElement>($"files/{fileId}/check_processing_status/");

		
		public Task<JsonElement> RetryFileProcessingAsync(int fileId) =>
			PostAt<JsonElement>($"files/{fileId}/retry_processing/", new { });

		private async Task<T> GetAt<T>(string path)
		{
			var stopwatch = Stopwatch.StartNew();
            ArcGISEventLog.Write(ArcGISEventLog.EventType.Information,
				$"[GoPilot] GET start: {path}", flush: true);
			try
			{
				using var response = await _api.Raw.GetAsync(_api.GoPilotBaseUrl + path);
				var rawJson = await response.Content.ReadAsStringAsync();
                ArcGISEventLog.Write(ArcGISEventLog.EventType.Information,
					$"[GoPilot] GET response: path={path}, status={(int)response.StatusCode} " +
					$"({response.StatusCode}), elapsedMs={stopwatch.ElapsedMilliseconds}", flush: true);
				if (!response.IsSuccessStatusCode)
                    ArcGISEventLog.Write(ArcGISEventLog.EventType.Error,
						$"[GoPilot] GET error body: {rawJson}", flush: true);

				response.EnsureSuccessStatusCode();
				return JsonSerializer.Deserialize<T>(rawJson);
			}
			catch (System.Exception ex)
			{
                ArcGISEventLog.Write(ArcGISEventLog.EventType.Error,
					$"[GoPilot] GET failed: path={path}, elapsedMs={stopwatch.ElapsedMilliseconds}\n{ex}",
					flush: true);
				throw;
			}
		}

		private async Task<T> PostAt<T>(string path, object payload, bool isDelete = false)
		{
			var method = isDelete ? "DELETE" : "POST";
			var stopwatch = Stopwatch.StartNew();
            ArcGISEventLog.Write(ArcGISEventLog.EventType.Information,
				$"[GoPilot] {method} start: {path}", flush: true);
			try
			{
				using var response = isDelete
					? await _api.Raw.DeleteAsync(_api.GoPilotBaseUrl + path)
					: await _api.Raw.PostAsJsonAsync(_api.GoPilotBaseUrl + path, payload);
				var rawJson = await response.Content.ReadAsStringAsync();
                ArcGISEventLog.Write(ArcGISEventLog.EventType.Information,
					$"[GoPilot] {method} response: path={path}, " +
					$"status={(int)response.StatusCode} ({response.StatusCode}), " +
					$"elapsedMs={stopwatch.ElapsedMilliseconds}", flush: true);
				if (!response.IsSuccessStatusCode)
                    ArcGISEventLog.Write(ArcGISEventLog.EventType.Error,
						$"[GoPilot] request error body: {rawJson}", flush: true);

				response.EnsureSuccessStatusCode();
				return JsonSerializer.Deserialize<T>(rawJson);
			}
			catch (System.Exception ex)
			{
                ArcGISEventLog.Write(ArcGISEventLog.EventType.Error,
					$"[GoPilot] request failed: path={path}, elapsedMs={stopwatch.ElapsedMilliseconds}\n{ex}",
					flush: true);
				throw;
			}
		}
	}
}
