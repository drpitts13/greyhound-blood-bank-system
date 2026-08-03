using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Commonly published US blood-supplier ISBT Product Description Codes (PDCs).
/// Not a full ICCBBA catalog — facility must replace/extend with ICCBBA-validated data.
/// </summary>
public static class UsSupplierProductCodeSeed
{
    public const string StandardVersion = "US-PUBLIC-SUBSET-PENDING-ICCBBA";

    public static IReadOnlyList<IsbtProductCode> CreateRows() =>
    [
        // Whole blood
        Row("E0009", "WHOLE BLOOD|CPD/450mL/refg", ComponentClass.WholeBlood),
        Row("E0023", "WHOLE BLOOD|CPD/500mL/refg", ComponentClass.WholeBlood),
        Row("E0033", "WHOLE BLOOD|CPD/500mL/refg|ResLeu:<5E6", ComponentClass.WholeBlood),
        Row("E0053", "WHOLE BLOOD|CPDA-1/450mL/refg", ComponentClass.WholeBlood),

        // Red blood cells
        Row("E0150", "RED BLOOD CELLS|CPD/450mL/refg", ComponentClass.RedBloodCells),
        Row("E0179", "RED BLOOD CELLS|CPD/500mL/refg|Irradiated|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E0181", "RED BLOOD CELLS|CPD/500mL/refg|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E0195", "RED BLOOD CELLS|CPDA-1/450mL/refg", ComponentClass.RedBloodCells),
        Row("E0206", "RED BLOOD CELLS|CPDA-1/450mL/refg|Irradiated", ComponentClass.RedBloodCells),
        Row("E0209", "RED BLOOD CELLS|CPDA-1/450mL/refg|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E0226", "RED BLOOD CELLS|CPDA-1/500mL/refg|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E0332", "RED BLOOD CELLS|CPD>AS1/500mL/refg|Irradiated|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E0336", "RED BLOOD CELLS|CPD>AS1/500mL/refg|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E0382", "RED BLOOD CELLS|CP2D>AS3/500mL/refg|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E0385", "RED BLOOD CELLS|CPD>AS5/450mL/refg", ComponentClass.RedBloodCells),
        Row("E0401", "RED BLOOD CELLS|CPD>AS5/450mL/refg|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E0424", "RED BLOOD CELLS|CPD>AS5/500mL/refg|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E0685", "Apheresis RED BLOOD CELLS|CP2D>AS3/XX/refg|ResLeu:<5E6|1st container", ComponentClass.RedBloodCells),
        Row("E0686", "Apheresis RED BLOOD CELLS|CP2D>AS3/XX/refg|ResLeu:<5E6|2nd container", ComponentClass.RedBloodCells),
        Row("E4520", "Deglycerolized RED BLOOD CELLS|None/XX/refg|Open", ComponentClass.RedBloodCells),
        Row("E4545", "Apheresis RED BLOOD CELLS|ACD-A>AS3/XX/refg|ResLeu:<5E6|2nd container", ComponentClass.RedBloodCells),
        Row("E5085", "Frozen RED BLOOD CELLS|None/XX/<-65C|ResLeu:<5E6", ComponentClass.RedBloodCells),
        Row("E5160", "Washed RED BLOOD CELLS|None/XX/refg|Open", ComponentClass.RedBloodCells),
        Row("E5169", "Washed RED BLOOD CELLS|None/XX/refg|Open|ResLeu:<5E6", ComponentClass.RedBloodCells),

        // Plasma / FFP
        Row("E0701", "FRESH FROZEN PLASMA|CPD/XX/<=-18C", ComponentClass.Plasma),
        Row("E0707", "FRESH FROZEN PLASMA|CPDA-1/XX/<=-18C", ComponentClass.Plasma),
        Row("E0869", "Apheresis FRESH FROZEN PLASMA|ACD-A/XX/<=-18C", ComponentClass.Plasma),
        Row("E2553", "PLASMA|CPD/XX/<=-18C|Cryo reduced", ComponentClass.Plasma),

        // Platelets
        Row("E2807", "PLATELETS|CPD/450mL/20-24C", ComponentClass.Platelets),
        Row("E2940", "Apheresis PLATELETS|ACD-A/XX/20-24C", ComponentClass.Platelets),
        Row("E3077", "Apheresis PLATELETS|ACD-A/XX/20-24C|ResLeu:<5E6", ComponentClass.Platelets),
        Row("E3087", "Apheresis PLATELETS|ACD-A/XX/20-24C|ResLeu:<5E6|1st container", ComponentClass.Platelets),
        Row("E3088", "Apheresis PLATELETS|ACD-A/XX/20-24C|ResLeu:<5E6|2nd container", ComponentClass.Platelets),
        Row("E3089", "Apheresis PLATELETS|ACD-A/XX/20-24C|ResLeu:<5E6|3rd container", ComponentClass.Platelets),
        Row("E3102", "Apheresis PLATELETS|ACD-A/XX/20-24C|1st container", ComponentClass.Platelets),
        Row("E3103", "Apheresis PLATELETS|ACD-A/XX/20-24C|2nd container", ComponentClass.Platelets),
        Row("E4635", "Apheresis PLATELETS|ACD-A/XX/20-24C|<3E11 plts", ComponentClass.Platelets),
        Row("E4643", "Apheresis PLATELETS|ACD-A/XX/20-24C|ResLeu:<5E6|<3E11 plts", ComponentClass.Platelets),
        Row("E4644", "Apheresis PLATELETS|ACD-A/XX/20-24C|ResLeu:<5E6|1st container|<3E11 plts", ComponentClass.Platelets),
        Row("E4645", "Apheresis PLATELETS|ACD-A/XX/20-24C|ResLeu:<5E6|2nd container|<3E11 plts", ComponentClass.Platelets),
        Row("E6001", "POOLED PLATELETS|CPD/XX/20-24C|ResLeu:<5E6|Bacterial test", ComponentClass.Platelets),

        // Cryoprecipitate
        Row("E5165", "CRYOPRECIPITATE|None/XX/<=-18C", ComponentClass.Cryoprecipitate),
    ];

    private static IsbtProductCode Row(string pdc, string description, ComponentClass componentClass) =>
        new()
        {
            ProductDescriptionCode = pdc,
            Description = description,
            ComponentClass = componentClass.ToString(),
            AttributesJson = "[]",
            RequiresExtendedDivision = false,
            StandardVersion = StandardVersion,
            IsPlaceholder = true
        };
}
