using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// A supplier order still in Draft has not gone to the supplier (end-user testing pass 295: a special order for
/// Demo-Special, Avery's irradiated, CMV-negative red cells read only "Draft" after seven days, nothing saying it was
/// never sent or for whom).
/// </summary>
public static class SupplierOrderDraftText
{
    /// <summary>"Not sent · 7 d · for Demo-Special, Avery (MRN0023)"; null when the order is not a draft.</summary>
    public static string? Badge(SupplierOrderDto order, DateTime nowUtc)
    {
        if (order.Status != SupplierOrderStatus.Draft)
        {
            return null;
        }

        var created = DateTime.SpecifyKind(order.CreatedUtc, DateTimeKind.Utc);
        var age = ElapsedText.FromMinutes((int)Math.Max(0, (nowUtc - created).TotalMinutes));
        var patients = order.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.PatientName))
            .Select(l => string.IsNullOrWhiteSpace(l.PatientMrn) ? l.PatientName!.Trim() : $"{l.PatientName!.Trim()} ({l.PatientMrn.Trim()})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return $"Not sent · {age}" + (patients.Count == 0 ? string.Empty : $" · for {string.Join(", ", patients)}");
    }
}
