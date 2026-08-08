using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Rasid.Models;

namespace Rasid.Services
{
	//wraps the ApiClient class and provides methods for interacting with the Rasid API
	internal class RasidApiClient
	{
		private readonly ApiClient _api;
		public RasidApiClient(ApiClient api) { _api = api; }

		public Task<UserProfile> GetProfileAsync() =>
			_api.GetAsync<UserProfile>("accounts/profile/");

		public Task<List<ProjectItem>> GetUserProjectsAsync(bool hidden = false) =>
			_api.GetAsync<List<ProjectItem>>($"projects/?hidden={hidden.ToString().ToLower()}");

		public Task<List<SolutionItem>> GetSolutionsAsync() =>
			_api.GetAsync<List<SolutionItem>>("solutions/");

		public Task<List<ProcessItem>> GetProcessesAsync(string projectSlug) =>
			_api.GetAsync<List<ProcessItem>>($"processes/?project={projectSlug}");

		public Task<ProjectItem> CreateProjectAsync(string solutionSlug, string title, List<int> tags = null)
		{
			var payload = new Dictionary<string, object> { ["solution_slug"] = solutionSlug, ["title"] = title };
			if (tags != null) payload["tags"] = tags;
			return _api.PostJsonAsync<ProjectItem>("projects-create/", payload);
		}

		public Task<JsonElement> GetProcessConfigAsync(string projectSlug) =>
			_api.GetAsync<JsonElement>($"projects/{projectSlug}/process-config/");

		public async Task<JsonElement> SearchCatalogueAsync(
			Dictionary<string, object> payload,
			bool isPlanet = false)
		{
			if (payload.TryGetValue("bbox", out var bbox) && bbox is IEnumerable<double> list)
				payload["bbox"] = JsonSerializer.Serialize(list);

			using var form = new FormUrlEncodedContent(ToStringDict(payload));
			var response = isPlanet
				? await PostPlanetCatalogueAsync(form)
				: await _api.Raw.PostAsync(
					_api.BaseUrl + "sentinel2-catalogue/",
					form);
			if (!response.IsSuccessStatusCode)
				throw new Exception(await ExtractDetailAsync(response));
			return await response.Content.ReadFromJsonAsync<JsonElement>();
		}

		private async Task<HttpResponseMessage> PostPlanetCatalogueAsync(
			HttpContent form)
		{
			var handler = new HttpClientHandler
			{
				CookieContainer = new CookieContainer()
			};
			using var client = new HttpClient(handler);
			client.DefaultRequestHeaders.Authorization =
				_api.Raw.DefaultRequestHeaders.Authorization;

			var apiRoot = new Uri(_api.ApiHost + "/");
			using var csrfResponse = await client.GetAsync(
				new Uri(apiRoot, "api/auth/csrf/"));
			csrfResponse.EnsureSuccessStatusCode();

			var csrfToken = handler.CookieContainer
				.GetCookies(apiRoot)["csrftoken_v2"]?.Value;
			if (string.IsNullOrWhiteSpace(csrfToken))
				throw new InvalidOperationException(
					"The Planet catalogue did not provide a CSRF token.");

			using var request = new HttpRequestMessage(
				HttpMethod.Post,
				new Uri(apiRoot, "workspace/planet_catalogue/"));
			request.Headers.Referrer = apiRoot;
			request.Headers.TryAddWithoutValidation("Origin", _api.ApiHost);
			request.Headers.TryAddWithoutValidation("X-CSRFToken", csrfToken);
			request.Content = form;

			return await client.SendAsync(request);
		}

		public async Task<ProcessItem> CreateProcessAsync(string projectSlug, Dictionary<string, object> payload,
			Dictionary<string, string> filePaths = null)
		{
			using var form = new MultipartFormDataContent();
			foreach (var kv in payload)
				form.Add(new StringContent(kv.Value?.ToString() ?? ""), kv.Key);

			if (filePaths != null)
			{
				foreach (var kv in filePaths)
				{
					var fileContent = new StreamContent(File.OpenRead(kv.Value));
					fileContent.Headers.ContentType =
						new MediaTypeHeaderValue("image/tiff");
					form.Add(fileContent, kv.Key, Path.GetFileName(kv.Value));
				}
			}

			var response = await _api.Raw.PostAsync(
				_api.BaseUrl + $"projects/{projectSlug}/processes/", form);
			if (response.StatusCode != System.Net.HttpStatusCode.Created)
				throw new Exception(await ExtractDetailAsync(response));
			return await response.Content.ReadFromJsonAsync<ProcessItem>();
		}

		public async Task HideProcessAsync(string projectSlug, int processId)
		{
			var response = await _api.Raw.PostAsync(
				_api.BaseUrl + $"projects/{projectSlug}/processes/{processId}/hide/",
				new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
			if (!response.IsSuccessStatusCode) throw new Exception(await ExtractDetailAsync(response));
		}

		public async Task HideProjectAsync(string projectSlug)
		{
			var response = await _api.Raw.PostAsync(
				_api.BaseUrl + $"projects/{projectSlug}/hide/",
				new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
			if (!response.IsSuccessStatusCode) throw new Exception(await ExtractDetailAsync(response));
		}

		public Task<JsonElement> GetProcessDetailAsync(int processId) =>
			_api.GetAsync<JsonElement>($"process/detail/?id={processId}");

		public Task<JsonElement> SubmitFeedbackAsync(object feedbackData) =>
			_api.PostJsonAsync<JsonElement>("accounts/feedback/", feedbackData);

		public async Task<string> DownloadFileAsync(
	string url,
	string destDir = null)
		{
			if (string.IsNullOrWhiteSpace(url))
				throw new Exception("No file URL was provided.");

			Uri requestUri;

			if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
			{
				requestUri = absoluteUri;
			}
			else
			{
				// Handles /media/file.zip and media/file.zip
				requestUri = new Uri(
					new Uri(_api.ApiHost),
					url.StartsWith("/") ? url : "/" + url);
			}

			var apiUri = new Uri(_api.ApiHost);
			var isRasidApiDomain = string.Equals(
				requestUri.Host,
				apiUri.Host,
				StringComparison.OrdinalIgnoreCase);


			if (isRasidApiDomain && requestUri.Scheme != apiUri.Scheme)
			{
				requestUri = new UriBuilder(requestUri)
				{
					Scheme = apiUri.Scheme,
					Port = apiUri.IsDefaultPort ? -1 : apiUri.Port
				}.Uri;
			}

			var isRasidApiHost = string.Equals(
				requestUri.Host,
				apiUri.Host,
				StringComparison.OrdinalIgnoreCase) &&
				requestUri.Scheme == apiUri.Scheme &&
				requestUri.Port == apiUri.Port;

			using var externalClient =
				isRasidApiHost ? null : new HttpClient();

			using var response = isRasidApiHost
				? await _api.Raw.GetAsync(
					requestUri,
					HttpCompletionOption.ResponseHeadersRead)
				: await externalClient!.GetAsync(
					requestUri,
					HttpCompletionOption.ResponseHeadersRead);

			if (!response.IsSuccessStatusCode)
			{
				var responseBody = await response.Content.ReadAsStringAsync();

				if (responseBody.Length > 500)
					responseBody = responseBody[..500];

				var safeUrl = requestUri.GetLeftPart(UriPartial.Path);

				throw new Exception(
					$"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})\n" +
					$"URL: {safeUrl}\n" +
					$"Server response: {responseBody}");
			}

			var filename = response.Content.Headers.ContentDisposition?.FileNameStar
				?? response.Content.Headers.ContentDisposition?.FileName
				?? Path.GetFileName(requestUri.AbsolutePath);
			filename = Path.GetFileName(filename.Trim('"'));

			if (string.IsNullOrWhiteSpace(filename) ||
				filename == "." ||
				filename == "..")
			{
				filename = "rasid_download";
			}

			destDir ??= Path.Combine(
				Path.GetTempPath(),
				"rasid_downloads");

			Directory.CreateDirectory(destDir);

			var filepath = Path.Combine(destDir, filename);

			var realDestination = Path.GetFullPath(destDir);
			var realFilepath = Path.GetFullPath(filepath);

			if (!realFilepath.StartsWith(
					realDestination + Path.DirectorySeparatorChar,
					StringComparison.OrdinalIgnoreCase))
			{
				throw new Exception(
					"Security: invalid download file path.");
			}

			await using var fileStream = File.Create(filepath);

			await response.Content.CopyToAsync(fileStream);

			return filepath;
		}

		private static Dictionary<string, string> ToStringDict(Dictionary<string, object> d)
		{
			var result = new Dictionary<string, string>();
			foreach (var kv in d) result[kv.Key] = kv.Value?.ToString() ?? "";
			return result;
		}

		private static async Task<string> ExtractDetailAsync(HttpResponseMessage response)
		{
			try
			{
				var json = await response.Content.ReadFromJsonAsync<JsonElement>();
				return json.TryGetProperty("detail", out var d) ? d.ToString() : json.ToString();
			}
			catch { return "Request failed"; }
		}
	}
}
