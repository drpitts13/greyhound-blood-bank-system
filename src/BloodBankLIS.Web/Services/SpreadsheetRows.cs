using ClosedXML.Excel;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The cells of an uploaded spreadsheet as text, row by row: the first worksheet's used range, each cell as the
/// spreadsheet shows it. Used to read an antigram saved as .xlsx with the same layout as the CSV import.
/// </summary>
public static class SpreadsheetRows
{
    public static IReadOnlyList<IReadOnlyList<string>> ReadXlsx(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault();
        var range = sheet?.RangeUsed();
        if (range is null)
        {
            return [];
        }

        var firstColumn = range.FirstColumn().ColumnNumber();
        var lastColumn = range.LastColumn().ColumnNumber();
        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in range.Rows())
        {
            var cells = new List<string>(lastColumn - firstColumn + 1);
            for (var c = firstColumn; c <= lastColumn; c++)
            {
                cells.Add(row.Worksheet.Cell(row.RowNumber(), c).GetFormattedString());
            }

            rows.Add(cells);
        }

        return rows;
    }
}
