using System.Text;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// Renders a <see cref="LabelDocument"/> to Zebra Programming Language (ZPL II) for
/// thermal label printers. Field data is escaped so caret/tilde control characters in
/// clinical text cannot corrupt the output or inject commands.
/// </summary>
public sealed class ZplLabelRenderer : ILabelRenderer
{
    public LabelFormat Format => LabelFormat.Zpl;

    public string Render(LabelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder();
        sb.Append("^XA");
        sb.Append("^PW").Append(document.WidthDots);
        sb.Append("^LL").Append(document.HeightDots);
        sb.Append("^LH0,0");

        foreach (var element in document.Elements)
        {
            switch (element)
            {
                case LabelText text:
                    sb.Append("^FO").Append(text.X).Append(',').Append(text.Y);
                    sb.Append("^A0N,").Append(text.FontHeight).Append(',').Append(text.FontWidth);
                    sb.Append("^FH^FD").Append(Escape(text.Value)).Append("^FS");
                    break;

                case LabelBarcode barcode:
                    sb.Append("^FO").Append(barcode.X).Append(',').Append(barcode.Y);
                    sb.Append("^BY2");
                    sb.Append("^BCN,").Append(barcode.Height).Append(',').Append(barcode.PrintInterpretation ? 'Y' : 'N').Append(",N,N");
                    sb.Append("^FH^FD").Append(Escape(barcode.Data)).Append("^FS");
                    break;
            }
        }

        sb.Append("^XZ");
        return sb.ToString();
    }

    /// <summary>
    /// Escapes control characters to their hex form for use with <c>^FH</c> (underscore
    /// indicator). '^', '~', '\' and the indicator '_' itself are escaped so clinical
    /// field data is always treated as literal text, never as ZPL commands.
    /// </summary>
    internal static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '_':
                    sb.Append("_5F");
                    break;
                case '^':
                    sb.Append("_5E");
                    break;
                case '~':
                    sb.Append("_7E");
                    break;
                case '\\':
                    sb.Append("_5C");
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        return sb.ToString();
    }
}
