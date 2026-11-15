using System.Linq.Expressions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Interfaces;

/// <summary>
/// Finding messages in the HL7 log (end-user testing pass 96: the log listed every message ever received or sent, with no
/// way to find one patient's, one order's or one control id's messages). Text matches the control id or anything in the
/// message (MRN, name, order or unit number), ignoring case; type, direction and status narrow it; the newest come first,
/// up to a limit.
/// </summary>
public static class Hl7MessageSearch
{
    public const int DefaultTake = 200;
    public const int MaxTake = 1000;

    public static int Take(int? take) => take is int t && t > 0 ? Math.Min(t, MaxTake) : DefaultTake;

    public static Expression<Func<Hl7MessageLog, bool>> Filter(string? text, string? messageType, Hl7Direction? direction, Hl7MessageStatus? status)
    {
        var term = string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToUpperInvariant();
        var type = string.IsNullOrWhiteSpace(messageType) ? null : messageType.Trim().ToUpperInvariant();
        return m =>
            (term == null || m.MessageControlId.ToUpper().Contains(term) || m.RawMessage.ToUpper().Contains(term))
            && (type == null || m.MessageType.ToUpper() == type)
            && (direction == null || m.Direction == direction)
            && (status == null || m.Status == status);
    }
}
