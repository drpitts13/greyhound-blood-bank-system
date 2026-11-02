using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-356: the chart's blood type history row says where a type came from and whether it counts.
public class BloodTypeDtoTests
{
    [Fact]
    public void RetiredTestedType_IsMarked_AndDoesNotCount()
    {
        var dto = BloodTypeDto.From(new PatientBloodTypeHistory
        {
            Abo = AboGroup.A, RhD = RhType.Positive, Source = BloodTypeSource.TestResult,
            InvalidatedUtc = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc), Reason = "Source result corrected: tube read as O."
        });
        Assert.True(dto.Invalidated);
        Assert.False(dto.CountsAsDetermination);
        Assert.Equal("Tested result", dto.SourceLabel);
    }

    [Fact]
    public void DocumentedManualType_NamesItsRecord_AndCounts()
    {
        var dto = BloodTypeDto.From(new PatientBloodTypeHistory
        {
            Abo = AboGroup.O, RhD = RhType.Negative, Source = BloodTypeSource.ManualEntry, IsCurrent = true,
            ManualDocumentation = ManualTypeDocumentation.PreviousBloodBankSystem, ManualDocumentationReference = "Legacy LIS record 1029"
        });
        Assert.True(dto.CountsAsDetermination);
        Assert.Equal("Manual, from a previous blood bank system: Legacy LIS record 1029", dto.SourceLabel);
    }
}
