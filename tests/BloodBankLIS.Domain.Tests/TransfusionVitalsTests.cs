using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class TransfusionVitalsTests
{
    // TEST-BB-998: bedside vitals are optional; a mistyped value (not a body temperature in °C, an impossible pulse, a
    // blood pressure with one number or diastolic above systolic) is named; they read as "T 37.0 °C · P 82 · BP 120/80 ·
    // RR 16", round-trip as JSON, and the temperature change from before is stated, not judged.
    [Fact]
    public void Vitals_AreCheckedDescribedAndCompared()
    {
        Assert.True(new TransfusionVitals().IsEmpty);
        Assert.Null(new TransfusionVitals().ToJson());
        Assert.Empty(new TransfusionVitals(37.0m, 82, 120, 80, 16).Problems("Before"));

        var typos = new TransfusionVitals(370m, 8, 120, null, 2).Problems("After");
        Assert.Equal(4, typos.Count);
        Assert.Contains(typos, p => p.StartsWith("After: temperature 370"));
        Assert.Contains(typos, p => p.Contains("both blood pressure numbers"));
        Assert.Contains(new TransfusionVitals(SystolicBp: 80, DiastolicBp: 120).Problems("Before"), p => p.Contains("80/120 cannot be right"));

        var before = new TransfusionVitals(37.0m, 82, 120, 80, 16);
        Assert.Equal("T 37.0 °C · P 82 · BP 120/80 · RR 16", before.Describe());
        Assert.Equal(before, TransfusionVitals.FromJson(before.ToJson()));
        Assert.Null(TransfusionVitals.FromJson("not json"));

        Assert.Equal("Temperature +1.4 °C from before the transfusion",
            TransfusionVitals.TemperatureChange(before, new TransfusionVitals(TemperatureC: 38.4m)));
        Assert.Null(TransfusionVitals.TemperatureChange(before, new TransfusionVitals(Pulse: 90)));
    }
}
