using Rasid.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Rasid.Services
{
	internal static class GoPilotFileLinkParser
	{
		private static readonly HashSet<string> SupportedExtensions =
			new(StringComparer.OrdinalIgnoreCase)
			{
				"tif", "tiff", "png", "jpg", "jpeg",
				"shp", "geojson", "gpkg", "zip"
			};

		private static readonly HashSet<string> ImageExtensions =
			new(StringComparer.OrdinalIgnoreCase)
			{
				"png", "jpg", "jpeg"
			};

		private static readonly Regex LinkPattern = new(
			@"\[(?<label>[^\]]*)\]\((?<markdown>(?:https?://|/)[^\s)]+)\)|(?<plain>https?://[^\s<>""']+)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase,
			TimeSpan.FromSeconds(1));

		private static readonly Regex ApiDownloadPath = new(
			@"/api/llm/files/(?<id>\d+)/download/?$",
			RegexOptions.Compiled | RegexOptions.IgnoreCase,
			TimeSpan.FromSeconds(1));

		public static IReadOnlyList<ChatFileAttachment> Extract(string text)
		{
			var files = new List<ChatFileAttachment>();
			if (string.IsNullOrWhiteSpace(text))
				return files;

			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Match match in LinkPattern.Matches(text))
			{
				var rawUrl = match.Groups["markdown"].Success
					? match.Groups["markdown"].Value
					: match.Groups["plain"].Value.TrimEnd('.', ',', ';', ':', '!', '?');

				if (!TryCreateAbsoluteUri(rawUrl, out var uri))
					continue;

				var label = match.Groups["label"].Value;
				var extension = Path.GetExtension(uri.AbsolutePath).TrimStart('.');
				if (!SupportedExtensions.Contains(extension))
					extension = Path.GetExtension(label).TrimStart('.');

				var apiDownload = ApiDownloadPath.Match(uri.AbsolutePath);
				if ((!SupportedExtensions.Contains(extension) && !apiDownload.Success) ||
					!seen.Add(uri.AbsoluteUri))
					continue;

				var fileName = Uri.UnescapeDataString(
					Path.GetFileName(uri.AbsolutePath));
				if (apiDownload.Success)
					fileName = Path.GetFileName(label);
				if (string.IsNullOrWhiteSpace(fileName) ||
					string.Equals(fileName, "download", StringComparison.OrdinalIgnoreCase))
					fileName = $"rasid_file_{apiDownload.Groups["id"].Value}";
				if (string.IsNullOrWhiteSpace(fileName))
					fileName = $"rasid_result.{extension}";

				var isImage = ImageExtensions.Contains(extension);
				files.Add(new ChatFileAttachment
				{
					Url = uri.AbsoluteUri,
					FileName = fileName,
					Extension = extension.ToLowerInvariant(),
					IsImage = isImage,
					ActionText = isImage
						? $"Save {fileName}"
						: $"Download {fileName}"
				});
			}

			return files;
		}

		public static bool IsAuthenticatedDownload(Uri uri) =>
			uri != null && ApiDownloadPath.IsMatch(uri.AbsolutePath);

		public static string HideFilesWhileProcessing(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return "Files are still processing...";

			foreach (Match match in LinkPattern.Matches(text))
			{
				var rawUrl = match.Groups["markdown"].Success
					? match.Groups["markdown"].Value
					: match.Groups["plain"].Value;
				if (!TryCreateAbsoluteUri(rawUrl, out var uri))
					continue;

				var extension = Path.GetExtension(uri.AbsolutePath).TrimStart('.');
				if (!SupportedExtensions.Contains(extension))
					extension = Path.GetExtension(match.Groups["label"].Value).TrimStart('.');
				if (!SupportedExtensions.Contains(extension) &&
					!IsAuthenticatedDownload(uri))
					continue;

				var lineStart = text.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
				text = text[..lineStart].TrimEnd();
				break;
			}

			return text + "\n\nFiles are still processing. Downloads will appear when ready.";
		}

		private static bool TryCreateAbsoluteUri(string url, out Uri uri)
		{
			if (Uri.TryCreate(url, UriKind.Absolute, out uri) &&
				(uri.Scheme == Uri.UriSchemeHttp ||
				 uri.Scheme == Uri.UriSchemeHttps))
				return true;

			if (url.StartsWith("/", StringComparison.Ordinal))
			{
				uri = new Uri(
					new Uri(ApiClient.Instance.ApiHost),
					url);
				return true;
			}

			uri = null;
			return false;
		}
	}
}
