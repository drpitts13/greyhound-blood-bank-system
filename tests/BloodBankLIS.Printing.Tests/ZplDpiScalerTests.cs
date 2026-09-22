using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Printing.Tests;

public class ZplDpiScalerTests
{
    [Fact]
    public void Scale_Leaves203DpiUnchanged()
    {
        var zpl = new ZplLabelRenderer().Render(new LabelDocument(406, 203, new LabelElement[]
        {
            new LabelText(10, 12, "A")
        })).Text!;

        Assert.Equal(zpl, ZplDpiScaler.Scale(zpl, 203));
    }

    [Fact]
    public void Scale_AdjustsDotCommandsFor300Dpi()
    {
        var zpl = "^XA^PW406^LL203^FO10,12^A0N,28,28^FH^FDHello^FS^BY2^BCN,80,Y,N,N^XZ";
        var scaled = ZplDpiScaler.Scale(zpl, 300);

        Assert.Contains("^PW600", scaled);
        Assert.Contains("^FDHello^FS", scaled);
        Assert.DoesNotContain("^PW406", scaled);
    }
}
