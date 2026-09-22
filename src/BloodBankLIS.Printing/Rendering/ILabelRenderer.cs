using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// Renders a <see cref="LabelDocument"/> to a concrete output. ZPL is the thermal
/// target, PDF is the on-screen preview, and the text preview is a plain proof.
/// Selection is by <see cref="Format"/> so new renderers are additive (docs A.2).
/// </summary>
public interface ILabelRenderer
{
    LabelFormat Format { get; }

    LabelRenderResult Render(LabelDocument document);
}
