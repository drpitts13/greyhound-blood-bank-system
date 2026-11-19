using BloodBankLIS.Domain.Instruments;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// What to do with an open instrument failure, and where to do it (end-user testing pass 215: message 8, "Test DAT is
/// not ordered on specimen ACC-DEMO-RCV1", offered only Replay and Resolve, with no way to reach the specimen or the
/// patient from the message). Wording only; the failure and its replay rules are unchanged.
/// </summary>
public static class InstrumentFailureNextStep
{
    public sealed record Link(string Text, string Href);

    public sealed record Step(string Text, IReadOnlyList<Link> Links);

    public static Step? For(string? errorDetail, string? specimenId, string? patientId, DateTime? specimenExpiredUtc = null)
    {
        if (string.IsNullOrWhiteSpace(errorDetail))
        {
            return null;
        }

        var links = new List<Link>();
        if (!string.IsNullOrWhiteSpace(specimenId))
        {
            var accession = specimenId.Trim();
            links.Add(new Link($"Specimen {accession} on Test Worklist", $"/test-worklist?accession={Uri.EscapeDataString(accession)}"));
        }

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            var id = patientId.Trim();
            links.Add(new Link($"Find patient {id}", $"/patients?q={Uri.EscapeDataString(id)}"));
        }

        if (errorDetail.Contains(InstrumentTranslationKeys.NotOrderedCode, StringComparison.Ordinal) && specimenExpiredUtc is DateTime expired)
        {
            // End-user testing pass 284: message 8's specimen, ACC-DEMO-RCV1, expired the night the DAT came in, so ordering
            // the test and replaying would be refused by the specimen check.
            return new Step(
                $"Specimen {specimenId?.Trim()} expired {expired.ToLocalTime():yyyy-MM-dd HH:mm}, so this result can no longer be posted to it (except to a transfusion reaction workup order, OCD-058). Resolve this message with the reason, and test a new specimen if the test is still needed.",
                links.Where(l => l.Href.StartsWith("/patients", StringComparison.Ordinal)).ToList());
        }

        if (errorDetail.Contains(InstrumentTranslationKeys.NotOrderedCode, StringComparison.Ordinal))
        {
            // The order is placed on the patient's chart; the tube's worklist lists only tests already ordered.
            return new Step(
                "If the test should be done on this specimen, order it on the patient's chart, then Replay. If it was run in error, resolve this message with the reason.",
                links.Where(l => l.Href.StartsWith("/patients", StringComparison.Ordinal)).ToList());
        }

        if (errorDetail.Contains(InstrumentTranslationKeys.PatientMismatchCode, StringComparison.Ordinal))
        {
            return new Step(
                "The instrument's patient ID is not the specimen's patient. Check the tube and its label against the specimen before anything posts, following your procedure; resolve this message with what was found.",
                // The ID is the one the instrument sent, not the specimen's patient: say so on the link.
                links.Select(l => l.Text.StartsWith("Find patient ", StringComparison.Ordinal)
                    ? l with { Text = $"Patient the instrument sent ({l.Text["Find patient ".Length..]})" }
                    : l).ToList());
        }

        if (errorDetail.Contains(InstrumentTranslationKeys.UnknownCode, StringComparison.Ordinal))
        {
            return new Step(
                "Add a translation for the instrument code, then Replay.",
                [new Link("Unmapped values", "/admin/hl7/translations?tab=unmapped")]);
        }

        return null;
    }
}
