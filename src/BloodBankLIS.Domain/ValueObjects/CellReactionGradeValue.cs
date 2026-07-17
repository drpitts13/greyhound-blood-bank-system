using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>Maps reaction grades to the short codes stored on ABORH panel subtests.</summary>
public static class CellReactionGradeValue
{
    public static string ToCode(CellReactionGrade grade) => grade switch
    {
        CellReactionGrade.NotTested => "NT",
        CellReactionGrade.Zero => "0",
        CellReactionGrade.OnePlus => "1+",
        CellReactionGrade.TwoPlus => "2+",
        CellReactionGrade.ThreePlus => "3+",
        CellReactionGrade.FourPlus => "4+",
        CellReactionGrade.Hemolysis => "H",
        CellReactionGrade.WeakPositive => "w+",
        CellReactionGrade.Mixed => "+/-",
        _ => "NT"
    };

    public static bool TryParseCode(string? code, out CellReactionGrade grade)
    {
        grade = CellReactionGrade.NotTested;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        grade = code.Trim() switch
        {
            "NT" or "nt" or "NotTested" => CellReactionGrade.NotTested,
            "0" => CellReactionGrade.Zero,
            "1+" => CellReactionGrade.OnePlus,
            "2+" => CellReactionGrade.TwoPlus,
            "3+" => CellReactionGrade.ThreePlus,
            "4+" => CellReactionGrade.FourPlus,
            "H" or "h" => CellReactionGrade.Hemolysis,
            "w+" or "W+" => CellReactionGrade.WeakPositive,
            "+/-" => CellReactionGrade.Mixed,
            _ => CellReactionGrade.NotTested
        };

        return code.Trim() is "NT" or "nt" or "NotTested" or "0" or "1+" or "2+" or "3+" or "4+"
            or "H" or "h" or "w+" or "W+" or "+/-";
    }
}
