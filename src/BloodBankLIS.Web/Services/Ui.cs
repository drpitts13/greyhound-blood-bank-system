using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>Presentation helpers: maps domain enums to badge styles and labels.</summary>
public static class Ui
{
    public static string Badge(UnitStatus status) => status switch
    {
        UnitStatus.Available => "badge badge-green",
        UnitStatus.Allocated => "badge badge-blue",
        UnitStatus.Issued => "badge badge-amber",
        UnitStatus.Transfused => "badge badge-gray",
        UnitStatus.Quarantine or UnitStatus.Received or UnitStatus.OnHold or UnitStatus.Missing => "badge badge-amber",
        UnitStatus.Discarded or UnitStatus.Expired or UnitStatus.ReturnedToSupplier or UnitStatus.Damaged => "badge badge-red",
        UnitStatus.Returned => "badge badge-gray",
        _ => "badge badge-gray"
    };

    public static string Badge(SpecimenStatus status) => status switch
    {
        SpecimenStatus.Accepted => "badge badge-green",
        SpecimenStatus.Received or SpecimenStatus.Collected => "badge badge-blue",
        SpecimenStatus.Rejected or SpecimenStatus.Expired or SpecimenStatus.Cancelled => "badge badge-red",
        _ => "badge badge-gray"
    };

    public static string Badge(ResultStatus status) => status switch
    {
        ResultStatus.Verified => "badge badge-green",
        ResultStatus.Entered => "badge badge-blue",
        ResultStatus.Corrected => "badge badge-amber",
        _ => "badge badge-gray"
    };

    public static string Badge(BillingEventStatus status) => status switch
    {
        BillingEventStatus.Pending => "badge badge-amber",
        BillingEventStatus.Reviewed => "badge badge-blue",
        BillingEventStatus.Exported => "badge badge-green",
        BillingEventStatus.Cancelled => "badge badge-red",
        _ => "badge badge-gray"
    };

    public static string Badge(Hl7MessageStatus status) => status switch
    {
        Hl7MessageStatus.Processed or Hl7MessageStatus.Acked => "badge badge-green",
        Hl7MessageStatus.Received or Hl7MessageStatus.Replayed => "badge badge-blue",
        Hl7MessageStatus.Errored or Hl7MessageStatus.Nacked => "badge badge-red",
        _ => "badge badge-gray"
    };

    public static string Badge(PrintJobStatus status) => status switch
    {
        PrintJobStatus.Printed => "badge badge-green",
        PrintJobStatus.Queued => "badge badge-blue",
        PrintJobStatus.Failed => "badge badge-red",
        _ => "badge badge-gray"
    };

    public static string Badge(ProductAllocationDisplayStatus status) => status switch
    {
        ProductAllocationDisplayStatus.ReadyForIssue => "badge badge-green",
        ProductAllocationDisplayStatus.Exception => "badge badge-red",
        ProductAllocationDisplayStatus.Reserved => "badge badge-amber",
        _ => "badge"
    };

    public static string Badge(AllocationStatus status) => status switch
    {
        AllocationStatus.Reserved => "badge badge-blue",
        AllocationStatus.Consumed => "badge badge-green",
        AllocationStatus.Released or AllocationStatus.Expired => "badge badge-gray",
        _ => "badge badge-gray"
    };

    public static string Badge(OrderStatus status) => status switch
    {
        OrderStatus.New or OrderStatus.InProcess or OrderStatus.Collected or OrderStatus.Received => "badge badge-blue",
        OrderStatus.Completed or OrderStatus.PartiallyComplete => "badge badge-green",
        OrderStatus.Cancelled or OrderStatus.Discontinued => "badge badge-red",
        OrderStatus.OnHold => "badge badge-amber",
        _ => "badge badge-gray"
    };

    public static string BloodType(AboGroup abo, RhType rh)
    {
        var group = abo == AboGroup.Unknown ? "?" : abo.ToString();
        var sign = rh switch { RhType.Positive => "+", RhType.Negative => "\u2212", _ => "?" };
        return $"{group}{sign}";
    }
}
