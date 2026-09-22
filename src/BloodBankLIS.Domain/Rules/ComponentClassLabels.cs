using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

public static class ComponentClassLabels
{
    public static string Label(ComponentClass componentClass) => componentClass switch
    {
        ComponentClass.RedBloodCells => "RBC",
        ComponentClass.Plasma => "FFP",
        ComponentClass.Platelets => "Platelets",
        ComponentClass.Cryoprecipitate => "Cryoprecipitate",
        ComponentClass.WholeBlood => "Whole Blood",
        ComponentClass.Granulocytes => "Granulocytes",
        _ => "Other"
    };
}
