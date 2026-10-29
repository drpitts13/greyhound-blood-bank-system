using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BloodBankLIS.Help;

/// <summary>Renders a help guide to a letter-size PDF with a cover, contents, and figures.</summary>
public static class ManualPdfRenderer
{
    private const string Disclaimer =
        "Compliance-supporting transfusion-service software. Not FDA-cleared, AABB-accredited, or CAP-certified by publication of this guide.";

    static ManualPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Render(HelpCatalog catalog, string guide)
    {
        var canonical = HelpGuides.Canonical(guide);
        var topics = catalog.InGuide(canonical).ToList();
        if (topics.Count == 0)
            throw new InvalidOperationException($"The {HelpGuides.Title(canonical)} has no topics.");

        var title = HelpGuides.Title(canonical);
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(64);
                page.DefaultTextStyle(style => style.FontSize(11));
                page.Content().Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("Greyhound Blood Bank LIS").FontSize(14).FontColor(Colors.Red.Darken3);
                    column.Item().PaddingTop(48).Text(title).FontSize(28).Bold();
                    column.Item().Text("Hospital transfusion-service laboratory information system").FontSize(13).Italic();
                    column.Item().PaddingTop(28).Text(Disclaimer).FontSize(10);
                    column.Item().Text(
                        "This guide describes the screens in the application. Facility procedures, medical-director policy, and the validation record still govern clinical use. " +
                        "Where a behavior depends on a local decision, the topic says so.");
                    column.Item().PaddingTop(12).Text(
                        "Figures show the development training database. They are not patient records from a clinical service.");
                });
            });

            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginHorizontal(54);
                page.MarginTop(48);
                page.MarginBottom(42);
                page.DefaultTextStyle(style => style.FontSize(11).LineHeight(1.25f));
                page.Header().PaddingBottom(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Text(text =>
                {
                    text.Span("Greyhound Blood Bank LIS  ·  " + title).FontSize(9).FontColor(Colors.Grey.Darken2);
                });
                page.Footer().PaddingTop(6).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).Row(row =>
                {
                    row.RelativeItem().Text(Disclaimer).FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.ConstantItem(70).AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1));
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
                page.Content().PaddingTop(12).Column(column =>
                {
                    column.Spacing(4);
                    column.Item().PaddingBottom(8).Text("Contents").FontSize(20).Bold();
                    string? chapter = null;
                    foreach (var topic in topics)
                    {
                        if (!string.Equals(chapter, topic.Chapter, StringComparison.Ordinal))
                        {
                            chapter = topic.Chapter;
                            column.Item().PaddingTop(8).Text(chapter).FontSize(12).Bold().FontColor(Colors.Red.Darken3);
                        }

                        var sectionId = SectionId(topic);
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().SectionLink(sectionId).Text(topic.Title);
                            row.ConstantItem(36).AlignRight().Text(text => text.BeginPageNumberOfSection(sectionId));
                        });
                    }

                    chapter = null;
                    foreach (var topic in topics)
                    {
                        if (!string.Equals(chapter, topic.Chapter, StringComparison.Ordinal))
                        {
                            chapter = topic.Chapter;
                            column.Item().PaddingTop(16).Text(chapter).FontSize(14).Bold().FontColor(Colors.Red.Darken3);
                        }

                        RenderTopic(column, catalog, topic);
                    }
                });
            });
        }).GeneratePdf();
    }

    private static void RenderTopic(ColumnDescriptor column, HelpCatalog catalog, HelpTopic topic)
    {
        var document = Markdig.Markdown.Parse(topic.Markdown, Pipeline);
        column.Item().PaddingTop(14).Section(SectionId(topic)).Text(topic.Title).FontSize(18).Bold();
        if (!string.IsNullOrWhiteSpace(topic.Summary))
            column.Item().PaddingBottom(4).Text(topic.Summary).Italic().FontColor(Colors.Grey.Darken2);
        foreach (var block in document)
            RenderBlock(column, catalog, block, 0);
    }

    private static void RenderBlock(ColumnDescriptor column, HelpCatalog catalog, Block block, int indent)
    {
        switch (block)
        {
            case HeadingBlock heading:
                column.Item().PaddingTop(8).PaddingLeft(indent).Text(text =>
                {
                    var span = text.Span(InlineText(heading.Inline));
                    span.Bold();
                    span.FontSize(heading.Level <= 2 ? 14 : 12);
                });
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(column, catalog, paragraph, indent);
                break;
            case QuoteBlock quote:
                RenderQuote(column, catalog, quote, indent);
                break;
            case ListBlock list:
                RenderList(column, catalog, list, indent);
                break;
            case ThematicBreakBlock:
                column.Item().PaddingVertical(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                break;
            case FencedCodeBlock or CodeBlock:
                column.Item().PaddingLeft(indent).Background(Colors.Grey.Lighten4).Padding(6).Text(CodeText(block)).FontSize(9);
                break;
            case Table table:
                RenderTable(column, table);
                break;
            case ContainerBlock container:
                foreach (var child in container)
                    RenderBlock(column, catalog, child, indent);
                break;
        }
    }

    private static void RenderParagraph(ColumnDescriptor column, HelpCatalog catalog, ParagraphBlock paragraph, int indent)
    {
        var images = new List<LinkInline>();
        var runs = new List<TextRun>();
        Collect(paragraph.Inline, false, false, false, runs, images);
        if (runs.Any(run => run.Text.Trim().Length > 0))
        {
            column.Item().PaddingTop(3).PaddingLeft(indent).Text(text =>
            {
                foreach (var run in runs)
                {
                    var span = text.Span(run.Text);
                    if (run.Bold)
                        span.Bold();
                    if (run.Italic)
                        span.Italic();
                }
            });
        }

        foreach (var image in images)
            RenderImage(column, catalog, image, indent);
    }

    private static void RenderImage(ColumnDescriptor column, HelpCatalog catalog, LinkInline image, int indent)
    {
        var alt = InlineText(image);
        if (catalog.TryGetFigure(image.Url ?? string.Empty, out var bytes) && bytes.Length > 0)
        {
            column.Item().PaddingTop(6).PaddingLeft(indent).MaxHeight(360).Image(bytes).FitArea();
            if (alt.Length > 0)
                column.Item().PaddingLeft(indent).Text(alt).FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
            return;
        }

        var caption = alt.Length > 0 ? alt : image.Url ?? "Figure";
        column.Item().PaddingTop(6).PaddingLeft(indent).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(8)
            .Text("Figure not yet captured: " + caption).FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
    }

    private static void RenderQuote(ColumnDescriptor column, HelpCatalog catalog, QuoteBlock quote, int indent)
    {
        var plain = string.Concat(quote.Select(block => block is LeafBlock leaf ? InlineText(leaf.Inline) : string.Empty)).TrimStart();
        var color = plain.StartsWith("Hard stop", StringComparison.OrdinalIgnoreCase) ? Colors.Red.Darken3
            : plain.StartsWith("Warning", StringComparison.OrdinalIgnoreCase) ? Colors.Orange.Darken3
            : Colors.Blue.Darken3;
        column.Item().PaddingTop(6).PaddingLeft(indent).BorderLeft(3).BorderColor(color).Background(Colors.Grey.Lighten4).Padding(8)
            .Column(inner =>
            {
                foreach (var child in quote)
                    RenderBlock(inner, catalog, child, 0);
            });
    }

    private static void RenderList(ColumnDescriptor column, HelpCatalog catalog, ListBlock list, int indent)
    {
        var index = 1;
        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem)
                continue;

            var marker = list.IsOrdered ? $"{index}." : "•";
            index++;
            var first = true;
            foreach (var child in listItem)
            {
                if (first && child is ParagraphBlock paragraph)
                {
                    var runs = new List<TextRun>();
                    var images = new List<LinkInline>();
                    Collect(paragraph.Inline, false, false, false, runs, images);
                    column.Item().PaddingTop(2).PaddingLeft(indent).Row(row =>
                    {
                        row.ConstantItem(18).Text(marker);
                        row.RelativeItem().Text(text =>
                        {
                            foreach (var run in runs)
                            {
                                var span = text.Span(run.Text);
                                if (run.Bold)
                                    span.Bold();
                                if (run.Italic)
                                    span.Italic();
                            }
                        });
                    });
                    foreach (var image in images)
                        RenderImage(column, catalog, image, indent + 18);
                    first = false;
                    continue;
                }

                first = false;
                RenderBlock(column, catalog, child, indent + 18);
            }
        }
    }

    private static void RenderTable(ColumnDescriptor column, Table table)
    {
        var rows = table.Cast<TableRow>().ToList();
        if (rows.Count == 0)
            return;

        column.Item().PaddingTop(6).Table(pdfTable =>
        {
            var columns = rows.Max(row => row.Count);
            pdfTable.ColumnsDefinition(definition =>
            {
                for (var i = 0; i < columns; i++)
                    definition.RelativeColumn();
            });

            foreach (var row in rows)
            {
                var header = row.IsHeader;
                foreach (var cell in row.Cast<TableCell>())
                {
                    var captured = cell;
                    pdfTable.Cell().Border(0.4f).BorderColor(Colors.Grey.Lighten1).Background(header ? Colors.Grey.Lighten3 : Colors.White)
                        .Padding(4).Text(text =>
                        {
                            var span = text.Span(BlockText(captured));
                            span.FontSize(9);
                            if (header)
                                span.Bold();
                        });
                }
            }
        });
    }

    private static void Collect(Inline? inline, bool bold, bool italic, bool code, List<TextRun> runs, List<LinkInline> images)
    {
        for (var node = inline; node is not null; node = node.NextSibling)
        {
            switch (node)
            {
                case LiteralInline literal:
                    runs.Add(new TextRun(literal.Content.ToString(), bold || code, italic || code));
                    break;
                case CodeInline codeInline:
                    runs.Add(new TextRun(codeInline.Content, true, false));
                    break;
                case EmphasisInline emphasis:
                    Collect(
                        emphasis.FirstChild,
                        bold || emphasis.DelimiterCount >= 2,
                        italic || emphasis.DelimiterCount % 2 == 1,
                        code,
                        runs,
                        images);
                    break;
                case LinkInline { IsImage: true } image:
                    images.Add(image);
                    break;
                case LinkInline link:
                    Collect(link.FirstChild, bold, italic, code, runs, images);
                    if (!string.IsNullOrWhiteSpace(link.Url) && (link.FirstChild is null))
                        runs.Add(new TextRun(link.Url, bold, italic));
                    break;
                case LineBreakInline:
                    runs.Add(new TextRun(" ", bold, italic));
                    break;
                case ContainerInline container:
                    Collect(container.FirstChild, bold, italic, code, runs, images);
                    break;
            }
        }
    }

    private static string InlineText(ContainerInline? inline)
    {
        var runs = new List<TextRun>();
        Collect(inline, false, false, false, runs, []);
        return string.Concat(runs.Select(run => run.Text)).Trim();
    }

    private static string BlockText(ContainerBlock block)
    {
        var parts = new List<string>();
        foreach (var child in block)
        {
            if (child is LeafBlock leaf)
                parts.Add(InlineText(leaf.Inline));
            else if (child is ContainerBlock container)
                parts.Add(BlockText(container));
        }

        return string.Join(" ", parts.Where(part => part.Length > 0));
    }

    private static string CodeText(Block block)
    {
        if (block is LeafBlock leaf && leaf.Lines.Count > 0)
            return leaf.Lines.ToString();
        return string.Empty;
    }

    private static string SectionId(HelpTopic topic) => topic.Guide + "-" + topic.Slug;

    private static readonly Markdig.MarkdownPipeline Pipeline = new Markdig.MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .Build();

    private sealed record TextRun(string Text, bool Bold, bool Italic);
}
