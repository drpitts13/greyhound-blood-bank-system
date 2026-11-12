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

    private static readonly AntibodyPanelLotListItemDto Lot =
        new(9, 1, "Panel Maker", "L-77", new DateOnly(2027, 1, 31), "Panel A", false, true, false);

    private static AntibodyPanelLotDetailDto Detail(AntibodyPanelLotListItemDto lot, params string[] cells) =>
        new(lot, cells.Select((n, i) => new AntibodyPanelLotCellDetailDto(i + 1, n, BloodBankLIS.Domain.Enums.PanelCellRole.Panel, i + 1,
            [new(1, "C", "anti-C", BloodBankLIS.Domain.Enums.AntigenExpression.Present)])).ToList(), [], []);

    private IRenderedComponent<AdminAntibodyPanelLots> ViewLot(AntibodyPanelLotListItemDto lot, AntibodyPanelLotDetailDto? corrected = null)
    {
        var responses = new Dictionary<string, object>
        {
            ["/api/admin/antibody-panel-lots"] = new List<AntibodyPanelLotListItemDto> { lot },
            ["/api/admin/antibody-panel-lots/manufacturers"] = new List<AntibodyPanelManufacturerListItemDto> { new(1, "MFR", "Panel Maker", true) },
            ["/api/admin/blood-attributes"] = new List<BloodAttributeDefinitionDto> { Antigen(1, "C", 1) },
            ["/api/admin/antibody-panel-lots/9"] = Detail(lot, "1", "2")
        };
        if (corrected is not null)
        {
            responses["/api/admin/antibody-panel-lots/9/antigram"] = corrected;
        }

        CannedApi.Register(this, responses);
        Services.AddSingleton(new ReportDownloadCache(new MemoryCache(new MemoryCacheOptions())));
        var page = Render<AdminAntibodyPanelLots>();
        page.WaitForAssertion(() => Assert.NotNull(page.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "View cells")));
        page.FindAll("button").First(b => b.TextContent.Trim() == "View cells").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-antigram-download]")));
        return page;
    }

    // TEST-BB-961: on a lot no workup has used, Correct antigram opens the cell editor filled with the lot's cells; saving
    // needs a reason, and the lot then shows the corrected cells.
    [Fact]
    public void CorrectAntigram_PrefillsAndSaves()
    {
        var page = ViewLot(Lot, Detail(Lot, "1", "2", "3"));
        page.Find("[data-antigram-correct]").Click();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-antigram-correct-form]")));
        Assert.Equal(["1", "2"], CellNumbers(page));
        Assert.DoesNotContain(page.FindAll("select"), x => x.InnerHtml.Contains("Select manufacturer"));
        Assert.True(page.Find("[data-antigram-correct-save]").HasAttribute("disabled"));

        page.Find("[data-antigram-correct-reason]").Input("Cell 2 C entered wrongly");
        Assert.False(page.Find("[data-antigram-correct-save]").HasAttribute("disabled"));
        page.Find("[data-antigram-correct-save]").Click();

        page.WaitForAssertion(() => Assert.Contains("Antigram corrected", page.Markup));
        Assert.Empty(page.FindAll("[data-antigram-correct-form]"));
        Assert.Equal(3, page.FindAll("td.mono").Count(td => td.TextContent is "1" or "2" or "3"));
    }

    // TEST-BB-962: once a workup has used the lot there is no Correct antigram; the page says to withdraw the lot and
    // create a corrected one.
    [Fact]
    public void LotInUse_CannotBeCorrected()
    {
        var page = ViewLot(Lot with { CompletedWorkupCount = 1 });
        Assert.Empty(page.FindAll("[data-antigram-correct]"));
        Assert.Contains("create a corrected lot", page.Find("[data-antigram-locked]").TextContent);
    }

    // TEST-BB-967: the panel lots page shows lots to replace, from the supply list, with what to do.
    [Fact]
    public void PanelLotsPage_ShowsLotsToReplace()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/antibody-panel-lots"] = new List<AntibodyPanelLotListItemDto> { Lot },
            ["/api/admin/antibody-panel-lots/manufacturers"] = new List<AntibodyPanelManufacturerListItemDto> { new(1, "MFR", "Panel Maker", true) },
            ["/api/admin/antibody-panel-lots/supply"] = new List<AntibodyPanelLotSupplyItemDto>
            {
                new("expiring", false, 9, "L-77", "Panel A", new DateOnly(2027, 1, 31), 3, null, true,
                    "Panel A lot L-77 expires in 3 days (2027-01-31); no other primary panel lot is entered to follow it.")
            }
        });
        Services.AddSingleton(new ReportDownloadCache(new MemoryCache(new MemoryCacheOptions())));
        var page = Render<AdminAntibodyPanelLots>();

        page.WaitForAssertion(() => Assert.Contains("Panel lots to replace", page.Find("[data-panel-supply]").TextContent));
        Assert.Contains("no other primary panel lot", page.Find("[data-panel-supply-item]").TextContent);
        Assert.Contains("Order the next lot", page.Find("[data-panel-supply]").TextContent);
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
