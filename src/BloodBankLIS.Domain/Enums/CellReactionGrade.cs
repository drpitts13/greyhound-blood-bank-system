namespace BloodBankLIS.Domain.Enums;

/// <summary>Standard agglutination / hemolysis reaction grades for cell typing subtests.</summary>
public enum CellReactionGrade
{
    NotTested = 0,
    Zero = 1,
    OnePlus = 2,
    TwoPlus = 3,
    ThreePlus = 4,
    FourPlus = 5,
    Hemolysis = 6,
    WeakPositive = 7,
    Mixed = 8
}
