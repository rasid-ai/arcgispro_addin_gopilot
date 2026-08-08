using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Rasid.Services
{
	internal static class ImageLoader
	{
		private static readonly string CacheDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Rasid", "ImageCache");

		public static string ResolveUrl(string url, string apiHost)
		{
			if (string.IsNullOrWhiteSpace(url)) return null;
			if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
				return absoluteUri.AbsoluteUri;

			return url.StartsWith("/") ? apiHost + url : apiHost + "/" + url;
		}

		private static string CachePath(string url)
		{
			using var sha = SHA256.Create();
			var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
			var ext = Path.GetExtension(url.Split('?')[0]);
			if (string.IsNullOrEmpty(ext)) ext = ".img";
			return Path.Combine(CacheDir, hash + ext);
		}
		public static async Task<BitmapImage> LoadAsync(string url, string apiHost, int? width = null, int? height = null)
		{
			var resolved = ResolveUrl(url, apiHost);
			if (resolved == null) return null;

			var cachePath = CachePath(resolved);
			byte[] bytes;

			if (File.Exists(cachePath))
			{
				bytes = await File.ReadAllBytesAsync(cachePath);
			}
			else
			{
				bytes = await DownloadAsync(resolved);
				if (bytes == null) return null;
				Directory.CreateDirectory(CacheDir);
				await File.WriteAllBytesAsync(cachePath, bytes);
			}

			return CreateBitmap(bytes, width, height);
		}

		private static async Task<byte[]> DownloadAsync(string url)
		{
			try
			{
				if (!Uri.TryCreate(url, UriKind.Absolute, out var imageUri))
					return null;

				var apiUri = new Uri(ApiClient.Instance.ApiHost);
				var isRasidApiHost =
					string.Equals(imageUri.Host, apiUri.Host, StringComparison.OrdinalIgnoreCase) &&
					imageUri.Scheme == apiUri.Scheme &&
					imageUri.Port == apiUri.Port;

				using var externalClient = isRasidApiHost ? null : new HttpClient();
				if (externalClient != null)
					externalClient.DefaultRequestHeaders.UserAgent.ParseAdd("RASID-ArcGISPro-Addin/1.0");

				using var response = isRasidApiHost
					? await ApiClient.Instance.Raw.GetAsync(imageUri)
					: await externalClient.GetAsync(imageUri);

				if (!response.IsSuccessStatusCode) return null;
				return await response.Content.ReadAsByteArrayAsync();
			}
			catch
			{
				return null; 
			}
		}

		private static BitmapImage CreateBitmap(byte[] bytes, int? width, int? height)
		{
			if (bytes == null || bytes.Length == 0)
				return null;

			using var stream = new MemoryStream(bytes);

			var bitmap = new BitmapImage();

			bitmap.BeginInit();
			bitmap.CacheOption = BitmapCacheOption.OnLoad;
			if (width > 0)
				bitmap.DecodePixelWidth = width.Value;
			else if (height > 0)
				bitmap.DecodePixelHeight = height.Value;
			bitmap.StreamSource = stream;
			bitmap.EndInit();
			bitmap.Freeze();

			return bitmap;
		}
	}
}
