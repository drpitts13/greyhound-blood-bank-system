namespace BloodBankLIS.Web.Services;

/// <summary>
/// Chart links that land on what they name: a tube's history on Specimens, one order, or one visit's orders. The chart
/// applies them each time the link changes, not only when the tab does.
/// </summary>
public static class PatientChartUrls
{
    public static string Specimen(long patientId, long specimenId) =>
        $"/patients/{patientId}?tab=specimens&specimen={specimenId}";

    /// <summary>The Orders tab searching for this order number; no number opens Orders unfiltered.</summary>
    public static string Order(long patientId, string? orderNumber) =>
        BloodBankLIS.Application.Search.UniversalLookupService.OrderHref(patientId, orderNumber);

    public static string VisitOrders(long patientId, long visitId) =>
        $"/patients/{patientId}?tab=orders&visit={visitId}";
}
