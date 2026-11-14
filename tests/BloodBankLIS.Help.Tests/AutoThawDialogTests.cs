using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The thaw prompt states the shelf life that applies and refuses before Yes (Cycle 685, OCD-068).</summary>
public class AutoThawDialogTests : BunitContext
{
    private static PatientAllocationRowDto Row(int? open, int? pooled, bool isPool) => new(
        1, 2, "W000000000001", "E0707", "Plasma", false, ProductAllocationDisplayStatus.ReadyForIssue, AllocationStatus.Reserved,
        null, null, null, null, null, DateTime.UtcNow, "tech", null,
        AutoThawProductCode: "E0707", AutoThawShelfLifeHours: 24,
        AutoThawOpenSystemShelfLifeHours: open, AutoThawPooledShelfLifeHours: pooled, AutoThawPooled: isPool);

    private IRenderedComponent<AutoThawDialog> Open(PatientAllocationRowDto row, Action? confirmed = null) => Render<AutoThawDialog>(p => p
        .Add(d => d.Row, row)
        .Add(d => d.Ask, true)
        .Add(d => d.ThawLocal, new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Local))
        .Add(d => d.OnConfirm, () => confirmed?.Invoke()));

    // TEST-BB-904: a closed single unit states the closed time and Yes thaws; ticking open system with no open-system time
    // set names the refusal and disables Yes, and clearing it brings Yes back.
    [Fact]
    public void OpenSystemNotSet_RefusesBeforeYes()
    {
        var confirmed = false;
        var dialog = Open(Row(null, null, isPool: false), () => confirmed = true);
        Assert.Contains("Expiration becomes 24 hours", dialog.Find("[data-auto-thaw-text]").TextContent);
        Assert.Empty(dialog.FindAll("[data-auto-thaw-refusal]"));

        dialog.Find("[data-auto-thaw-open]").Change(true);
        Assert.Contains("No open-system thawed shelf life is set for E0707", dialog.Find("[data-auto-thaw-refusal]").TextContent);
        Assert.True(dialog.Find("[data-auto-thaw-yes]").HasAttribute("disabled"));
        Assert.Contains("Expiration becomes not set", dialog.Find("[data-auto-thaw-text]").TextContent);

        dialog.Find("[data-auto-thaw-open]").Change(false);
        Assert.False(dialog.Find("[data-auto-thaw-yes]").HasAttribute("disabled"));
        dialog.Find("[data-auto-thaw-yes]").Click();
        Assert.True(confirmed);
    }

    // TEST-BB-905: a pool says so and states the pooled time, the shorter time when also opened; a pool whose code has
    // no pooled time is refused before Yes even with the box clear.
    [Fact]
    public void Pool_StatesThePooledTime_OrRefuses()
    {
        var dialog = Open(Row(4, 6, isPool: true));
        Assert.NotNull(dialog.Find("[data-auto-thaw-pooled]"));
        Assert.Contains("Expiration becomes 6 hours", dialog.Find("[data-auto-thaw-text]").TextContent);
        dialog.Find("[data-auto-thaw-open]").Change(true);
        Assert.Contains("Expiration becomes 4 hours", dialog.Find("[data-auto-thaw-text]").TextContent);

        var notSet = Open(Row(4, null, isPool: true));
        Assert.Contains("No pooled thawed shelf life is set for E0707", notSet.Find("[data-auto-thaw-refusal]").TextContent);
        Assert.True(notSet.Find("[data-auto-thaw-yes]").HasAttribute("disabled"));
    }
}
