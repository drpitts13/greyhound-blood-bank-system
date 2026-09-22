using System.Globalization;
using System.Text.RegularExpressions;

namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// Scales ZPL dot commands from the 203 dpi template grid to another print-head resolution.
/// Field data is left unchanged.
/// </summary>
public static partial class ZplDpiScaler
{
    public static string Scale(string zpl, int dpi)
    {
        if (string.IsNullOrEmpty(zpl) || dpi == 203)
        {
            return zpl;
        }

        if (dpi is < 100 or > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be between 100 and 600.");
        }

        var factor = dpi / 203d;
        int ScaleDots(int dots) => (int)Math.Round(dots * factor, MidpointRounding.AwayFromZero);

        zpl = Replace(zpl, Pw(), m => "^PW" + ScaleDots(Int(m, 1)));
        zpl = Replace(zpl, Ll(), m => "^LL" + ScaleDots(Int(m, 1)));
        zpl = Replace(zpl, Lh(), m => $"^LH{ScaleDots(Int(m, 1))},{ScaleDots(Int(m, 2))}");
        zpl = Replace(zpl, Fo(), m => $"^FO{ScaleDots(Int(m, 1))},{ScaleDots(Int(m, 2))}");
        zpl = Replace(zpl, Font(), m => $"^A0N,{ScaleDots(Int(m, 1))},{ScaleDots(Int(m, 2))}");
        zpl = Replace(zpl, Barcode(), m => "^BCN," + ScaleDots(Int(m, 1)));
        zpl = Replace(zpl, Module(), m => "^BY" + Math.Max(1, ScaleDots(Int(m, 1))));
        return zpl;
    }

    private static int Int(Match match, int group) =>
        int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    private static string Replace(string zpl, Regex regex, MatchEvaluator evaluator) =>
        regex.Replace(zpl, evaluator);

    [GeneratedRegex(@"\^PW(\d+)")]
    private static partial Regex Pw();

    [GeneratedRegex(@"\^LL(\d+)")]
    private static partial Regex Ll();

    [GeneratedRegex(@"\^LH(\d+),(\d+)")]
    private static partial Regex Lh();

    [GeneratedRegex(@"\^FO(\d+),(\d+)")]
    private static partial Regex Fo();

    [GeneratedRegex(@"\^A0N,(\d+),(\d+)")]
    private static partial Regex Font();

    [GeneratedRegex(@"\^BCN,(\d+)")]
    private static partial Regex Barcode();

    [GeneratedRegex(@"\^BY(\d+)")]
    private static partial Regex Module();
}
