namespace BloodBankLIS.Help.Tests;

public class LocalTimeSourceTests
{
    // TEST-BB-1224: the deviation text for a temperature excursion, the expiry status note, the reaction's closed and
    // reopened times and the last good backup give local time (end-user testing pass 202, after pass 189 missed them).
    [Fact]
    public void RemainingStaffTexts_GiveLocalTime()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var src = Path.Combine(dir!.FullName, "src");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([src, .. parts]));
        Assert.Contains("°C at {ClinicalTimeText.Local(readingUtc)} against a range", Read("BloodBankLIS.Application", "Inventory", "StorageTemperatureService.cs"));
        Assert.Contains("Reason = $\"Expired at {ClinicalTimeText.Local(unit.ExpiresUtc)}\"", Read("BloodBankLIS.Application", "Inventory", "InventoryService.cs"));
        var reactions = Read("BloodBankLIS.Web", "Components", "Pages", "Reactions.razor");
        Assert.DoesNotContain("HH:mm} UTC", reactions);
        Assert.DoesNotContain("HH:mm} UTC", Read("BloodBankLIS.Web", "Components", "Pages", "Admin", "AdminBackups.razor"));
    }
}
