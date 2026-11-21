using System.Linq.Expressions;
using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Instruments;

/// <summary>
/// Finding instrument messages (end-user testing pass 97: the list held the newest 200 only, with no way to find what an
/// analyzer sent for an older specimen). Text matches the specimen id, the patient id, or anything in the raw message,
/// ignoring case.
/// </summary>
public static class InstrumentMessageSearch
{
    public static Expression<Func<InstrumentMessageLog, bool>> Filter(long? interfaceId, string? text)
    {
        var term = string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToUpperInvariant();
        return m =>
            (interfaceId == null || m.InterfaceId == interfaceId)
            && (term == null
                || (m.SpecimenId != null && m.SpecimenId.ToUpper().Contains(term))
                || (m.PatientId != null && m.PatientId.ToUpper().Contains(term))
                || m.RawMessage.ToUpper().Contains(term));
    }
}
