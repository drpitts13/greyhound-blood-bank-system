using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// A specimen in the Compatibility pickers (end-user testing pass 225: an expired tube read "ACC0022 — EDTA (Expired
/// expires 2026-11-09 04:50)"). An expired one says when it expired; a usable one says its status and when it expires.
/// </summary>
public static class SpecimenChoiceText
{
    public static string For(string accession, string? specimenType, SpecimenStatus status, DateTime? expiresUtc, DateTime nowUtc)
    {
        var expired = status == SpecimenStatus.Expired || expiresUtc is DateTime e && e <= nowUtc;
        var when = expiresUtc is DateTime at ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : null;
        var state = expired
            ? when is null ? "expired" : $"expired {when}"
            : when is null ? Ui.EnumText(status) : $"{Ui.EnumText(status)}, expires {when}";
        return $"{accession} — {specimenType} ({state})";
    }
}
