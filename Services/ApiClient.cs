using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Rasid.Services
{
	internal class ApiClient
	{
		public static ApiClient Instance { get; } = new ApiClient();

		private readonly HttpClient _http = new HttpClient();
		public string BaseUrl { get; } = "https://api.rasid.ai/api/";       
		public string AppBaseUrl { get; } = "https://app.rasid.ai";         
		public string ApiHost { get; } = "https://api.rasid.ai";            
		public string GoPilotBaseUrl { get; } = "https://api.rasid.ai/api/llm/"; 
		public TimeSpan Timeout { get; } = TimeSpan.FromSeconds(15);      

		public void SetAuthHeader(string apiKey)
		{
			_http.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(apiKey)
				? null
				: new AuthenticationHeaderValue("Bearer", apiKey);
		}

		public async Task<T> GetAsync<T>(string path, TimeSpan? timeout = null)
		{
			using var cts = new System.Threading.CancellationTokenSource(timeout ?? Timeout);
			var response = await _http.GetAsync(BaseUrl + path, cts.Token);
			response.EnsureSuccessStatusCode();

			
			var rawJson = await response.Content.ReadAsStringAsync();
			
			return System.Text.Json.JsonSerializer.Deserialize<T>(rawJson);
		}

		public async Task<T> PostJsonAsync<T>(string path, object payload, TimeSpan? timeout = null)
		{
			using var cts = new System.Threading.CancellationTokenSource(timeout ?? Timeout);
			var response = await _http.PostAsJsonAsync(BaseUrl + path, payload, cts.Token);
			await EnsureSuccessOrThrowAsync(response);
			return await response.Content.ReadFromJsonAsync<T>();
		}

		public async Task<T> PostFormAsync<T>(string path, MultipartFormDataContent content, TimeSpan? timeout = null)
		{
			using var cts = new System.Threading.CancellationTokenSource(timeout ?? Timeout);
			var response = await _http.PostAsync(BaseUrl + path, content, cts.Token);
			await EnsureSuccessOrThrowAsync(response);
			return await response.Content.ReadFromJsonAsync<T>();
		}

		public HttpClient Raw => _http; // for streaming downloads in RasidApiClient.DownloadFileAsync

		private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
		{
			if (response.IsSuccessStatusCode) return;
			string detail;
			try
			{
				var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
				detail = json.TryGetProperty("detail", out var d) ? d.ToString() : json.ToString();
			}
			catch { detail = await response.Content.ReadAsStringAsync(); }
			throw new Exception(detail);
		}
	}
}