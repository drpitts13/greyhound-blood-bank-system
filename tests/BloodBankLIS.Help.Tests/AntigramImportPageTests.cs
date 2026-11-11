using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Web.Components.Pages.Admin;
using BloodBankLIS.Web.Services;
using Bunit;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>Import an antigram into a new panel lot, and download one (user request, 2026-11-11).</summary>
public class AntigramImportPageTests : BunitContext
{
    private static BloodAttributeDefinitionDto Antigen(long id, string code, int order) =>
        new(id, code, code, $"anti-{code}", true, order, 1, true, false, null, null, null);

    private IRenderedComponent<AdminAntibodyPanelLots> OpenNewLot()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/antibody-panel-lots"] = new List<AntibodyPanelLotListItemDto>(),
            ["/api/admin/antibody-panel-lots/manufacturers"] = new List<AntibodyPanelManufacturerListItemDto> { new(1, "MFR", "Panel Maker", true) },
            ["/api/admin/blood-attributes"] = new List<BloodAttributeDefinitionDto> { Antigen(1, "C", 1), Antigen(2, "c", 2), Antigen(5, "FYA", 3) }
        });
        Services.AddSingleton(new ReportDownloadCache(new MemoryCache(new MemoryCacheOptions())));
        var page = Render<AdminAntibodyPanelLots>();
        page.WaitForAssertion(() => Assert.NotNull(page.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "New lot")));
        page.FindAll("button").First(b => b.TextContent.Trim() == "New lot").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-antigram-import]")));
        return page;
    }

    private static List<string> CellNumbers(IRenderedComponent<AdminAntibodyPanelLots> page) =>
        page.FindAll("label").Where(l => l.TextContent.Trim() == "Cell")
            .Select(l => l.ParentElement!.QuerySelector("input")!.GetAttribute("value") ?? "").ToList();

    // TEST-BB-952: a CSV antigram fills the cells (the starting blank cell is replaced) and says how many cells and
    // antigens it read and which columns it left out.
    [Fact]
    public void CsvAntigram_FillsTheCells()
    {
        var page = OpenNewLot();
        page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(
            "Cell,C,c,Fy(a),Donor\n1,+,0,+het,A1\n2,0,+,0,B2\n3,w,+,+,C3\n", "panel.csv"));

        page.WaitForAssertion(() => Assert.Contains("3 cells and 3 antigens imported from panel.csv", page.Find("[data-antigram-import-result]").TextContent));
        Assert.Contains("left out: Donor", page.Find("[data-antigram-import-result]").TextContent);
        Assert.Equal(["1", "2", "3"], CellNumbers(page));
    }

    // TEST-BB-953: a file with a value that cannot be read is not used: the problems are listed and the cells are unchanged.
    [Fact]
    public void BadAntigram_IsNotUsed()
    {
        var page = OpenNewLot();
        var before = CellNumbers(page);
        page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("Cell,C\n7,maybe\n", "bad.csv"));

        page.WaitForAssertion(() => Assert.Contains("bad.csv was not used", page.Find("[data-antigram-import-result]").TextContent));
        Assert.Contains("\"maybe\" is not", page.Find("[data-antigram-import-result]").TextContent);
        Assert.Equal(before, CellNumbers(page));
    }

    // TEST-BB-954: an Excel (.xlsx) antigram in the same layout is read too.
    [Fact]
    public void ExcelAntigram_FillsTheCells()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Antigram");
        sheet.Cell(1, 1).Value = "Panel A lot 77";
        string[][] rows = [["Cell", "Role", "C", "c"], ["1", "Panel", "+", "0"], ["2", "Panel", "0", "+"]];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var page = OpenNewLot();
        page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(stream.ToArray(), "panel.xlsx"));

        page.WaitForAssertion(() => Assert.Contains("2 cells and 2 antigens imported from panel.xlsx", page.Find("[data-antigram-import-result]").TextContent));
        Assert.Equal(["1", "2"], CellNumbers(page));
    }

    // TEST-BB-957: a lot's antigram downloads in the import layout: Cell, Role, then its antigens in catalog order, one row
    // per cell, not-tested left blank.
    [Fact]
    public void LotAntigram_Downloads()
    {
        var lot = new AntibodyPanelLotListItemDto(9, 1, "Panel Maker", "L-77", new DateOnly(2027, 1, 31), "Panel A", false, true, false);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/antibody-panel-lots"] = new List<AntibodyPanelLotListItemDto> { lot },
            ["/api/admin/antibody-panel-lots/manufacturers"] = new List<AntibodyPanelManufacturerListItemDto> { new(1, "MFR", "Panel Maker", true) },
            ["/api/admin/blood-attributes"] = new List<BloodAttributeDefinitionDto> { Antigen(2, "c", 1), Antigen(1, "C", 2) },
            ["/api/admin/antibody-panel-lots/9"] = new AntibodyPanelLotDetailDto(lot,
            [
                new(1, "1", BloodBankLIS.Domain.Enums.PanelCellRole.Panel, 1,
                    [new(2, "c", "anti-c", BloodBankLIS.Domain.Enums.AntigenExpression.Absent), new(1, "C", "anti-C", BloodBankLIS.Domain.Enums.AntigenExpression.Present)]),
                new(2, "2", BloodBankLIS.Domain.Enums.PanelCellRole.Panel, 2,
                    [new(1, "C", "anti-C", BloodBankLIS.Domain.Enums.AntigenExpression.Weak)])
            ], [], [])
        });
        var cache = new ReportDownloadCache(new MemoryCache(new MemoryCacheOptions()));
        Services.AddSingleton(cache);
        var page = Render<AdminAntibodyPanelLots>();
        page.WaitForAssertion(() => Assert.NotNull(page.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "View cells")));
        page.FindAll("button").First(b => b.TextContent.Trim() == "View cells").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-antigram-download]")));

        page.Find("[data-antigram-download]").Click();
        var token = JSInterop.VerifyInvoke("open").Arguments[0]!.ToString()!.Split('/').Last();
        var file = cache.Take(token)!;
        Assert.Equal("antigram-Panel-Maker-L-77.csv", file.FileName);
        var lines = System.Text.Encoding.UTF8.GetString(file.Bytes).TrimStart('\uFEFF').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Cell,Role,c,C", "1,Panel,0,'+", "2,Panel,,w"], lines);
        // The '+ is the CSV guard against spreadsheet formulas; the import reads it back as present (TEST-BB-951).
    }

    // TEST-BB-955: the blank template opens as a download.
    [Fact]
    public void Template_OpensADownload()
    {
        var page = OpenNewLot();
        page.Find("[data-antigram-template]").Click();
        var open = JSInterop.VerifyInvoke("open");
        Assert.StartsWith("/downloads/reports/", open.Arguments[0]!.ToString());
    }
}
