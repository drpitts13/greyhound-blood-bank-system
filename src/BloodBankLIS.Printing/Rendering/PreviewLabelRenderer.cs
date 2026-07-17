using System.Text;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// Renders a <see cref="LabelDocument"/> to a plain-text proof for on-screen print
/// preview before committing to a physical printer (docs A.2). Not for production
/// printing; it exists so operators can verify content and layout.
/// </summary>
public sealed class PreviewLabelRenderer : ILabelRenderer
{
    public LabelFormat Format => LabelFormat.Preview;

    public string Render(LabelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder();
        sb.Append("LABEL ").Append(document.WidthDots).Append('x').Append(document.HeightDots).AppendLine(" dots");
        sb.AppendLine("----------------------------------------");

        foreach (var element in document.Elements.OrderBy(e => e.Y).ThenBy(e => e.X))
        {
            switch (element)
            {
                case LabelText text:
                    sb.Append('[').Append(text.X).Append(',').Append(text.Y).Append("] ");
                    sb.AppendLine(text.Bold ? text.Value.ToUpperInvariant() : text.Value);
                    break;

                case LabelBarcode barcode:
                    sb.Append('[').Append(barcode.X).Append(',').Append(barcode.Y).Append("] [|||| ")
                        .Append(barcode.Data).AppendLine(" ||||]");
                    break;
            }
        }

        return sb.ToString();
    }
}
