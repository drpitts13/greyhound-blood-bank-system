using BloodBankLIS.Application.Abstractions;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// Tabular PDF (QuestPDF) and Excel (ClosedXML) for operational reports.
/// </summary>
public sealed class TabularReportRenderer : IReportDocumentRenderer
{
    static TabularReportRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }

    public byte[] ToPdf(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string?>> rows, bool truncated)
    {
        var headers = columns.Count == 0 ? new[] { "Result" } : columns.ToArray();
        var body = columns.Count == 0
            ? new List<IReadOnlyList<string?>> { new[] { "No rows." } }
            : rows;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(style => style.FontSize(8));
                page.Header().Column(column =>
                {
                    column.Item().Text(title).FontSize(14).Bold();
                    column.Item().Text("Facility operational report. Not an FDA, AABB, or CAP compliance claim.").FontSize(8).Italic();
                    if (truncated)
                        column.Item().Text("Results were truncated at the export row limit.").FontSize(8);
                });
                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(definition =>
                    {
                        foreach (var _ in headers)
                            definition.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var headerText in headers)
                        {
                            header.Cell().Background("#1f4b6e").Padding(3)
                                .Text(headerText).FontColor(Colors.White).SemiBold();
                        }
                    });

                    foreach (var row in body)
                    {
                        for (var i = 0; i < headers.Length; i++)
                        {
                            var cell = i < row.Count ? row[i] : null;
                            table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).Padding(2)
                                .Text(cell ?? string.Empty);
                        }
                    }
                });
                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ToExcel(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string?>> rows, bool truncated)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = truncated
            ? "Results were truncated at the export row limit."
            : "Facility operational report. Not an FDA, AABB, or CAP compliance claim.";

        var headers = columns.Count == 0 ? new[] { "Result" } : columns;
        for (var c = 0; c < headers.Count; c++)
        {
            var cell = sheet.Cell(4, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
        }

        if (columns.Count == 0)
        {
            sheet.Cell(5, 1).Value = "No rows.";
        }
        else
        {
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < headers.Count; c++)
                    sheet.Cell(r + 5, c + 1).Value = c < rows[r].Count ? rows[r][c] ?? string.Empty : string.Empty;
            }
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
