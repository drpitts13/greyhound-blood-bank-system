using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Gives a reaction investigation a finished specimen workup straight in the database: a pre-transfusion specimen
/// collected before the issue, a post-transfusion specimen after it, each with a reaction workup order whose ABO/Rh
/// and DAT are verified. For tests about closing, whose subject is not the workup itself.
/// </summary>
internal static class ReactionWorkupTestData
{
    public static async Task CompleteAsync(
        BloodBankDbContext c,
        long investigationId,
        DateTime nowUtc,
        string postDat = "Negative",
        AboRh? type = null)
    {
        var inv = await c.ReactionInvestigations.FindAsync(investigationId)
            ?? throw new InvalidOperationException($"Investigation {investigationId} not found.");
        var key = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var encounter = new Encounter { PatientId = inv.PatientId, VisitNumber = $"V-RXW-{key}", Status = EncounterStatus.Active };
        var location = new OrderingLocation { Code = $"RXW-{key}", Name = "Workup test location", IsActive = true };
        c.Encounters.Add(encounter);
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();

        var aboRh = AboRhResultValue.Format(type ?? new AboRh(AboGroup.O, RhType.Positive));
        var (preSpecimen, preOrder) = await AddAsync(c, inv.PatientId, encounter.Id, location.Id, $"{key}-PRE", nowUtc.AddDays(-2), aboRh, "Negative");
        var (postSpecimen, postOrder) = await AddAsync(c, inv.PatientId, encounter.Id, location.Id, $"{key}-POST", nowUtc, aboRh, postDat);
        inv.PreTransfusionSpecimenId = preSpecimen;
        inv.PreTransfusionOrderId = preOrder;
        inv.PostTransfusionSpecimenId = postSpecimen;
        inv.PostTransfusionOrderId = postOrder;
        await c.SaveChangesAsync();
    }

    private static async Task<(long SpecimenId, long OrderId)> AddAsync(
        BloodBankDbContext c, long patientId, long encounterId, long locationId, string key, DateTime collectedUtc, string aboRh, string dat)
    {
        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-RXW-{key}",
            PatientId = patientId,
            EncounterId = encounterId,
            SpecimenType = "EDTA",
            CollectedUtc = collectedUtc,
            ExpiresUtc = collectedUtc.AddDays(3),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        var order = new Order
        {
            OrderNumber = $"RXW-{key}",
            PatientId = patientId,
            EncounterId = encounterId,
            OrderingLocationId = locationId,
            OrderCategory = OrderCategory.Test,
            OrderType = OrderType.TransfusionReactionWorkup,
            OrderName = "Transfusion reaction workup",
            Priority = OrderPriority.Stat,
            Status = OrderStatus.InProcess,
            Source = OrderSource.Manual,
            OrderedUtc = collectedUtc
        };
        c.Orders.Add(order);
        await c.SaveChangesAsync();

        c.OrderSpecimens.Add(new OrderSpecimen { OrderId = order.Id, SpecimenId = specimen.Id, IsPrimary = true });
        var line = 1;
        foreach (var (code, value) in new[] { ("ABORH", aboRh), ("DAT", dat) })
        {
            c.OrderLines.Add(new OrderLine
            {
                OrderId = order.Id, LineNumber = line++, LineCategory = OrderCategory.Test, LineName = code, TestCode = code,
                ResultStatus = ResultStatus.Verified
            });
            c.TestResults.Add(new TestResult
            {
                SpecimenId = specimen.Id, PatientId = patientId, OrderId = order.Id, TestCode = code, Value = value,
                Status = ResultStatus.Verified, EnteredBy = "tech1", EnteredUtc = collectedUtc, VerifiedBy = "tech2", VerifiedUtc = collectedUtc
            });
        }

        await c.SaveChangesAsync();
        return (specimen.Id, order.Id);
    }
}
