using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class HttpSecurityHeaderPolicyTests
{
    [Fact]
    public void ApiHeaders_DenyFramingAndSniffing()
    {
        Assert.Contains(HttpSecurityHeaderPolicy.ApiHeaders, h =>
            h.Name == "X-Frame-Options" && h.Value == "DENY");
        Assert.Contains(HttpSecurityHeaderPolicy.ApiHeaders, h =>
            h.Name == "X-Content-Type-Options" && h.Value == "nosniff");
        Assert.Contains(HttpSecurityHeaderPolicy.ApiHeaders, h =>
            h.Name == "Content-Security-Policy" && h.Value.Contains("frame-ancestors 'none'"));
        Assert.Contains(HttpSecurityHeaderPolicy.ApiHeaders, h =>
            h.Name == "Cache-Control" && h.Value == "no-store");
    }

    [Fact]
    public void WebHeaders_DenyFraming()
    {
        Assert.Contains(HttpSecurityHeaderPolicy.WebHeaders, h =>
            h.Name == "X-Frame-Options" && h.Value == "DENY");
        Assert.Contains(HttpSecurityHeaderPolicy.WebHeaders, h =>
            h.Name == "Content-Security-Policy" && h.Value.Contains("frame-ancestors 'none'"));
    }

    [Fact]
    public void AllowAnyOrigin_OutsideDevelopment_IsHardStop()
    {
        var result = HttpSecurityHeaderPolicy.EvaluateCorsAllowAny(true, isDevelopment: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(HttpSecurityHeaderPolicy.CorsAnyCode, result.Code);
    }

    [Fact]
    public void AllowAnyOrigin_InDevelopment_Passes()
    {
        var result = HttpSecurityHeaderPolicy.EvaluateCorsAllowAny(true, isDevelopment: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ExplicitOrigins_InProduction_Pass()
    {
        var result = HttpSecurityHeaderPolicy.EvaluateCorsAllowAny(false, isDevelopment: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
