using Markdig;
using Markdig.Syntax;
using System.Net;
using System.Text.RegularExpressions;

namespace Rasid.Services
{
	internal static class MarkdownService
	{
		private static readonly MarkdownPipeline Pipeline =
			new MarkdownPipelineBuilder()
				.UseAdvancedExtensions()
				.UseSoftlineBreakAsHardlineBreak()
				.Build();

		private static readonly Regex HtmlTagPattern = new(
			"<[^>]+>",
			RegexOptions.Compiled,
			System.TimeSpan.FromSeconds(1));

		private static readonly Regex WhitespacePattern = new(
			@"\s+",
			RegexOptions.Compiled,
			System.TimeSpan.FromSeconds(1));

		public static MarkdownDocument Parse(string markdown) =>
			Markdig.Markdown.Parse(markdown ?? string.Empty, Pipeline);

		public static string ToPlainText(string markdown)
		{
			if (string.IsNullOrWhiteSpace(markdown))
				return string.Empty;

			var text = Markdig.Markdown.ToPlainText(markdown, Pipeline);
			text = HtmlTagPattern.Replace(text, string.Empty);
			text = WebUtility.HtmlDecode(text);
			return WhitespacePattern.Replace(text, " ").Trim();
		}
	}
}
