namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The units waiting for an ABO/Rh retype are listed twice: on Inventory's Retype board and on the Test worklist's
/// product retype tab. End-user testing pass 151: the worklist tab lacked what the board had gained (expiry and time
/// waiting, pass 132; the patient a unit is reserved for, pass 150), so the bench saw less than the inventory desk.
/// </summary>
public class RetypeListParityTests
{
    // TEST-BB-1173: both lists show the reserved patient, the expiry note and the time waiting.
    [Theory]
    [InlineData("Inventory.razor", "row")]
    [InlineData("TestWorklist.razor", "item")]
    public void RetypeList_ShowsReservedPatient_Expiry_AndWaiting(string page, string variable)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components", "Pages", page));
        Assert.Contains($"{variable}.ReservedFor", text);
        Assert.Contains($"{variable}.ExpiryNote", text);
        Assert.Contains($"{variable}.WaitingNote", text);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root.");
    }
}
