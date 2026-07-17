using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// Renders a <see cref="LabelDocument"/> to a concrete output. ZPL is the production
/// target; a preview renderer produces a human-readable proof. Selection is by
/// <see cref="Format"/> so new renderers (e.g. PDF) are additive (docs A.2).
/// </summary>
public interface ILabelRenderer
{
    LabelFormat Format { get; }

    string Render(LabelDocument document);
}
