using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A named specimen label design, laid out on the label designer: its size, what it is for (the tube, or an aliquot
/// split from it), and where each barcode and data element sits. <see cref="DocumentJson"/> is a
/// <c>LabelLayoutDocument</c>. One active design per purpose is the default; the others are chosen when printing.
/// </summary>
public class SpecimenLabelDesign : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public SpecimenLabelPurpose Purpose { get; set; } = SpecimenLabelPurpose.Tube;

    public int WidthDots { get; set; }

    public int HeightDots { get; set; }

    public string DocumentJson { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; } = 1;
}
