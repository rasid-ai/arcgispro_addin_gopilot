using Markdig.Extensions.Tables;
using ReverseMarkdown;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Rasid.Services;
using System;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using MdBlock = Markdig.Syntax.Block;
using MdList = Markdig.Syntax.ListBlock;
using MdListItem = Markdig.Syntax.ListItemBlock;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using WpfInline = System.Windows.Documents.Inline;
using WpfList = System.Windows.Documents.List;
using WpfListItem = System.Windows.Documents.ListItem;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableRow = System.Windows.Documents.TableRow;

namespace Rasid.Pages.Shared
{
	/// <summary>
	/// Renders API Markdown as native, selectable WPF content.
	/// </summary>
	public class MarkdownViewer : FlowDocumentScrollViewer
	{
		private static readonly Regex HtmlTagPattern = new(
			"<[^>]+>",
			RegexOptions.Compiled,
			TimeSpan.FromSeconds(1));

		private static readonly Converter HtmlConverter = new();

		public static readonly DependencyProperty MarkdownProperty =
			DependencyProperty.Register(
				nameof(Markdown),
				typeof(string),
				typeof(MarkdownViewer),
				new PropertyMetadata(string.Empty, OnMarkdownChanged));

		public string Markdown
		{
			get => (string)GetValue(MarkdownProperty);
			set => SetValue(MarkdownProperty, value);
		}

		public MarkdownViewer()
		{
			IsToolBarVisible = false;
			VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
			Focusable = false;
			IsSelectionEnabled = true;
			Background = Brushes.Transparent;
			Padding = new Thickness(0);
		}

		private static void OnMarkdownChanged(
			DependencyObject dependencyObject,
			DependencyPropertyChangedEventArgs args)
		{
			if (dependencyObject is MarkdownViewer viewer)
				viewer.Render(args.NewValue as string ?? string.Empty);
		}

		private void Render(string markdown)
		{
			var document = new FlowDocument
			{
				PagePadding = new Thickness(0),
				ColumnWidth = double.PositiveInfinity,
				TextAlignment = TextAlignment.Left
			};
			Emoji.Wpf.Behaviors.SetEmojiRendering(document, true);
			document.SetBinding(
				TextElement.FontFamilyProperty,
				new Binding(nameof(FontFamily)) { Source = this });
			document.SetBinding(
				TextElement.FontSizeProperty,
				new Binding(nameof(FontSize)) { Source = this });
			document.SetBinding(
				TextElement.ForegroundProperty,
				new Binding(nameof(Foreground)) { Source = this });

			if (markdown.TrimStart().StartsWith("<", StringComparison.Ordinal))
				markdown = HtmlConverter.Convert(markdown);

			var parsed = MarkdownService.Parse(markdown);
			AppendBlocks(parsed, document.Blocks);
			if (document.Blocks.Count == 0)
				document.Blocks.Add(CreateParagraph());
			Document = document;
		}

		private void AppendBlocks(ContainerBlock source, BlockCollection target)
		{
			foreach (var block in source)
				AppendBlock(block, target);
		}

		private void AppendBlock(MdBlock block, BlockCollection target)
		{
			switch (block)
			{
				case HeadingBlock heading:
					target.Add(RenderHeading(heading));
					break;
				case ParagraphBlock paragraph:
					target.Add(RenderParagraph(paragraph));
					break;
				case QuoteBlock quote:
					target.Add(RenderQuote(quote));
					break;
				case MdList list:
					target.Add(RenderList(list));
					break;
				case MdTable table:
					target.Add(RenderTable(table));
					break;
				case CodeBlock code:
					target.Add(RenderCodeBlock(code));
					break;
				case ThematicBreakBlock:
					target.Add(new Paragraph
					{
						BorderBrush = new SolidColorBrush(Color.FromRgb(160, 166, 170)),
						BorderThickness = new Thickness(0, 0, 0, 1),
						FontSize = 1,
						Margin = new Thickness(0, 8, 0, 12)
					});
					break;
				case HtmlBlock html:
					AppendPlainHtml(html.Lines.ToString(), target);
					break;
				case ContainerBlock container:
					AppendBlocks(container, target);
					break;
			}
		}

		private Paragraph RenderHeading(HeadingBlock heading)
		{
			var paragraph = CreateParagraph();
			paragraph.Margin = new Thickness(0, heading.Level == 1 ? 12 : 10, 0, 8);
			paragraph.FontWeight = FontWeights.SemiBold;
			paragraph.FontSize = Math.Max(FontSize + 1, FontSize * (1.65 - heading.Level * 0.1));
			AppendInlines(heading.Inline, paragraph.Inlines);
			return paragraph;
		}

		private Paragraph RenderParagraph(ParagraphBlock paragraphBlock)
		{
			var paragraph = CreateParagraph();
			AppendInlines(paragraphBlock.Inline, paragraph.Inlines);
			return paragraph;
		}

		private Section RenderQuote(QuoteBlock quote)
		{
			var section = new Section
			{
				Margin = new Thickness(2, 3, 0, 5),
				Padding = new Thickness(8, 1, 0, 1),
				BorderThickness = new Thickness(2, 0, 0, 0),
				BorderBrush = new SolidColorBrush(Color.FromRgb(150, 160, 166)),
				FontStyle = FontStyles.Italic
			};
			AppendBlocks(quote, section.Blocks);
			return section;
		}

		private WpfList RenderList(MdList list)
		{
			var result = new WpfList
			{
				MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
				Margin = new Thickness(28, 1, 0, 10),
				Padding = new Thickness(0)
			};

			foreach (var itemBlock in list)
			{
				if (itemBlock is not MdListItem item)
					continue;

				var listItem = new WpfListItem
				{
					Margin = new Thickness(0, 0, 0, 2)
				};
				AppendBlocks(item, listItem.Blocks);
				result.ListItems.Add(listItem);
			}
			return result;
		}

		private WpfTable RenderTable(MdTable table)
		{
			var borderBrush = new SolidColorBrush(Color.FromRgb(216, 224, 228));
			var textBrush = new SolidColorBrush(Color.FromRgb(38, 50, 56));
			var result = new WpfTable
			{
				CellSpacing = 0,
				Margin = new Thickness(0, 6, 0, 10),
				Background = new SolidColorBrush(Color.FromRgb(247, 249, 250))
			};
			var rowGroup = new TableRowGroup();
			result.RowGroups.Add(rowGroup);

			foreach (var tableChild in table)
			{
				if (tableChild is not MdTableRow sourceRow)
					continue;

				var row = new WpfTableRow();
				if (sourceRow.IsHeader)
				{
					row.FontWeight = FontWeights.SemiBold;
					row.Background = new SolidColorBrush(Color.FromRgb(238, 243, 244));
				}

				foreach (var cellChild in sourceRow)
				{
					if (cellChild is not MdTableCell sourceCell)
						continue;

					var cell = new WpfTableCell
					{
						Padding = sourceRow.IsHeader
							? new Thickness(8, 6, 8, 6)
							: new Thickness(8, 5, 8, 5),
						BorderBrush = borderBrush,
						BorderThickness = new Thickness(0.5),
						Foreground = textBrush
					};
					AppendBlocks(sourceCell, cell.Blocks);
					if (cell.Blocks.Count == 0)
						cell.Blocks.Add(CreateParagraph());
					row.Cells.Add(cell);
				}
				rowGroup.Rows.Add(row);
			}
			return result;
		}

		private Paragraph RenderCodeBlock(CodeBlock code) =>
			new(new Run(code.Lines.ToString()))
			{
				FontFamily = new FontFamily("Consolas"),
				FontSize = Math.Max(10, FontSize - 1),
				Background = new SolidColorBrush(Color.FromArgb(28, 80, 80, 80)),
				Padding = new Thickness(7, 5, 7, 5),
				Margin = new Thickness(0, 4, 0, 6)
			};

		private void AppendInlines(ContainerInline container, InlineCollection target)
		{
			if (container == null)
				return;

			for (var inline = container.FirstChild; inline != null; inline = inline.NextSibling)
			{
				switch (inline)
				{
					case LiteralInline literal:
						target.Add(new Run(literal.Content.ToString()));
						break;
					case EmphasisInline emphasis:
						target.Add(RenderEmphasis(emphasis));
						break;
					case CodeInline code:
						target.Add(new Run(code.Content)
						{
							FontFamily = new FontFamily("Consolas"),
							Background = new SolidColorBrush(Color.FromArgb(28, 80, 80, 80))
						});
						break;
					case LineBreakInline:
						target.Add(new LineBreak());
						break;
					case LinkInline link:
						target.Add(RenderLink(link));
						break;
					case HtmlInline html:
						AppendPlainText(WebUtility.HtmlDecode(
							HtmlTagPattern.Replace(html.Tag, string.Empty)), target);
						break;
					case ContainerInline nested:
						AppendInlines(nested, target);
						break;
				}
			}
		}

		private Span RenderEmphasis(EmphasisInline emphasis)
		{
			Span span = emphasis.DelimiterCount >= 2 ? new Bold() : new Italic();
			AppendInlines(emphasis, span.Inlines);
			return span;
		}

		private WpfInline RenderLink(LinkInline link)
		{
			var label = new Span();
			AppendInlines(link, label.Inlines);
			if (label.Inlines.Count == 0)
				label.Inlines.Add(new Run(link.Url ?? string.Empty));
			if (link.IsImage)
				return label;

			var url = link.GetDynamicUrl != null ? link.GetDynamicUrl() : link.Url;
			if (!TryResolveUri(url, out var uri))
				return label;
			if (GoPilotFileLinkParser.IsAuthenticatedDownload(uri))
				return label;

			var hyperlink = new Hyperlink
			{
				NavigateUri = uri,
				Cursor = System.Windows.Input.Cursors.Hand,
				ToolTip = uri.AbsoluteUri
			};
			hyperlink.SetBinding(
				TextElement.ForegroundProperty,
				new Binding(nameof(Foreground)) { Source = this });
			while (label.Inlines.FirstInline != null)
				hyperlink.Inlines.Add(label.Inlines.FirstInline);
			hyperlink.RequestNavigate += (_, args) =>
			{
				Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri)
				{
					UseShellExecute = true
				});
				args.Handled = true;
			};
			return hyperlink;
		}

		private static Paragraph CreateParagraph() =>
			new() { Margin = new Thickness(0, 0, 0, 10) };

		private static void AppendPlainText(string text, InlineCollection target)
		{
			if (!string.IsNullOrEmpty(text))
				target.Add(new Run(text));
		}

		private void AppendPlainHtml(string html, BlockCollection target)
		{
			var text = WebUtility.HtmlDecode(
				HtmlTagPattern.Replace(html ?? string.Empty, string.Empty)).Trim();
			if (!string.IsNullOrEmpty(text))
				target.Add(new Paragraph(new Run(text))
				{
					Margin = new Thickness(0, 0, 0, 5)
				});
		}

		private static bool TryResolveUri(string url, out Uri uri)
		{
			if (Uri.TryCreate(url, UriKind.Absolute, out uri) &&
				(uri.Scheme == Uri.UriSchemeHttp ||
				 uri.Scheme == Uri.UriSchemeHttps))
				return true;

			if (!string.IsNullOrWhiteSpace(url) &&
				url.StartsWith("/", StringComparison.Ordinal))
			{
				uri = new Uri(new Uri(ApiClient.Instance.ApiHost), url);
				return true;
			}

			uri = null;
			return false;
		}
	}
}
