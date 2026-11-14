using System.Text.RegularExpressions;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Dropdown choices read as words, not code names: the issue form offered "EmergencyRelease" and "LabelIllegible"
/// (Cycle 588), and 74 dropdowns across the app showed enum names.
/// </summary>
public partial class EnumTextTests
{
    // TEST-BB-679
    [Fact]
    public void EnumText_ReadsAsWords()
    {
        Assert.Equal("Emergency release", Ui.EnumText(IssueType.EmergencyRelease));
        Assert.Equal("Massive transfusion", Ui.EnumText(IssueType.MassiveTransfusion));
        Assert.Equal("Standard", Ui.EnumText(IssueType.Standard));
        Assert.Equal("Label illegible", Ui.EnumText(UnitAppearance.LabelIllegible));
        Assert.Equal("CMV negative", Ui.EnumText(SpecialTransfusionRequirementType.CmvNegative));
        Assert.Equal("Antigen negative", Ui.EnumText(SpecialTransfusionRequirementType.AntigenNegative));
        Assert.Equal("AB", Ui.EnumText(AboGroup.AB));
        Assert.Equal("O", Ui.EnumText(AboGroup.O));
        Assert.Equal("Partially fulfilled", Ui.EnumText(FulfillmentStatus.PartiallyFulfilled));
        Assert.Equal("Pending Retype", Ui.EnumText(UnitStatus.Received));
        Assert.Equal("Shipped out", Ui.EnumText(UnitStatus.ShippedOut));
        Assert.Equal(BloodBankLIS.Domain.Rules.ComponentClassLabels.Label(ComponentClass.RedBloodCells), Ui.EnumText(ComponentClass.RedBloodCells));
        // Not an enum: as it is.
        Assert.Equal("ORD-1", Ui.EnumText("ORD-1"));
        Assert.Equal("42", Ui.EnumText(42));
        Assert.Equal(string.Empty, Ui.EnumText(null));
    }

    [Theory]
    [InlineData("Hl7Message", "HL7 message")]
    [InlineData("AboRhType", "ABO rh type")]
    [InlineData("RhDNegative", "RhD negative")]
    [InlineData("DatPositive", "DAT positive")]
    [InlineData("XMCompatible", "XM compatible")]
    [InlineData("PositiveIgG", "Positive IgG")]
    [InlineData("PositiveC3", "Positive C3")]
    [InlineData("InProgress", "In progress")]
    public void EnumText_KeepsAcronyms(string name, string expected) =>
        Assert.Equal(expected, Ui.CodeNameText(name));

    // TEST-BB-1011: antigen headers keep their case (c is not C) and Duffy, Kidd, Lewis, Lutheran, Kell Kp and Js
    // codes stored in capitals read as written (Fya, Jkb); end-user testing pass 9.
    [Theory]
    [InlineData("FYA", "Fya")]
    [InlineData("JKB", "Jkb")]
    [InlineData("Lea", "Lea")]
    [InlineData("c", "c")]
    [InlineData("C", "C")]
    [InlineData("K", "K")]
    [InlineData("M", "M")]
    [InlineData("FYX", "FYX")]
    public void AntigenText_WritesAntigensAsWritten(string code, string expected) =>
        Assert.Equal(expected, Ui.AntigenText(code));

    // TEST-BB-680: a dropdown whose option text is the same value as its option value goes through Ui.EnumText, so an
    // enum never shows its code name.
    [Fact]
    public void Dropdowns_DoNotShowCodeNames()
    {
        var root = RepoRoot();
        var raw = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => RawOption().IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}")
            .ToList();
        Assert.True(raw.Count == 0, "Option text shows the raw value; use @Ui.EnumText(...):" + Environment.NewLine + string.Join(Environment.NewLine, raw));
    }

    // TEST-BB-681: a status badge, and a table cell showing a status, priority, source, category, or similar value, goes
    // through Ui.EnumText too ("InProcess" was shown as is), and a unit status reads in words ("Shipped out").
    [Fact]
    public void BadgesAndStatusCells_DoNotShowCodeNames()
    {
        Assert.Equal("Shipped out", Ui.StatusLabel(UnitStatus.ShippedOut));
        Assert.Equal("Returned to supplier", Ui.StatusLabel(UnitStatus.ReturnedToSupplier));
        Assert.Equal("Pending Retype", Ui.StatusLabel(UnitStatus.Received));

        var root = RepoRoot();
        var raw = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => RawBadge().IsMatch(l.Text) || RawStatusCell().IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}")
            .ToList();
        Assert.True(raw.Count == 0, "A badge or status cell shows the raw value; use @Ui.EnumText(...):" + Environment.NewLine + string.Join(Environment.NewLine, raw));
    }

    [GeneratedRegex("<option value=\"@([A-Za-z_][A-Za-z0-9_]*)\"[^>]*>@\\1</option>")]
    private static partial Regex RawOption();

    [GeneratedRegex("class=\"@Ui\\.Badge\\(([^()]*(?:\\([^()]*\\))?[^()]*)\\)\"[^>]*>@\\(?\\1\\)?</span>")]
    private static partial Regex RawBadge();

    [GeneratedRegex(">@[A-Za-z_][A-Za-z0-9_]*\\.(Status|Priority|Source|OrderCategory|IssueType|Category|Severity|Direction|Level|DisplayStatus|FinalDisposition|ResultStatus|FulfillmentStatus|EncounterType|LineCategory|Kind)<")]
    private static partial Regex RawStatusCell();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
