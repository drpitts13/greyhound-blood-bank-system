namespace BloodBankLIS.Help.Tests;

/// <summary>The builder pages describe freeze dating the way the rule applies it (loop 2 tick 16).</summary>
public class FreezeDatingWordingTests
{
    // TEST-BB-1398: Expiration Codes and Modification Rules both say the source-expiration cap does not apply to a freeze
    // (ModificationExpirationRule.CapsAtSourceExpiration), so an admin setting up frozen red cells is not told their code
    // cannot work.
    [Theory]
    [InlineData("AdminExpirationModificationCodes.razor")]
    [InlineData("AdminModificationRules.razor")]
    public void BuilderPages_SayAFreezeIsNotCapped(string page)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var text = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Pages", "Admin", page));
        Assert.Contains("except a freeze", text);
    }
}
