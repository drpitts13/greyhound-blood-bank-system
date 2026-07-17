using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Rules.PatientWorkspace;

public static class OrderValidator
{
    public static RuleEvaluation Validate(
        Order order,
        IReadOnlyList<OrderLine> activeLines,
        bool patientExists,
        bool encounterExists,
        bool encounterBelongsToPatient,
        bool orderingLocationExists,
        bool orderingLocationActive)
    {
        var results = new List<RuleResult>();

        if (!patientExists)
        {
            results.Add(RuleResult.HardStop("ORDER.PATIENT.NOTFOUND", "Patient not found."));
        }

        if (order.EncounterId <= 0)
        {
            results.Add(RuleResult.HardStop("ORDER.ENCOUNTER.REQUIRED", "A visit is required for every order."));
        }
        else if (!encounterExists)
        {
            results.Add(RuleResult.HardStop("ORDER.ENCOUNTER.NOTFOUND", "Visit not found."));
        }
        else if (!encounterBelongsToPatient)
        {
            results.Add(RuleResult.HardStop("ORDER.ENCOUNTER.PATIENT.MISMATCH", "Visit does not belong to this patient."));
        }

        if (order.OrderingLocationId <= 0)
        {
            results.Add(RuleResult.HardStop("ORDER.LOCATION.REQUIRED", "Ordering location is required."));
        }
        else if (!orderingLocationExists)
        {
            results.Add(RuleResult.HardStop("ORDER.LOCATION.NOTFOUND", "Ordering location not found."));
        }
        else if (!orderingLocationActive)
        {
            results.Add(RuleResult.HardStop("ORDER.LOCATION.INACTIVE", "Ordering location is inactive."));
        }

        if (string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            results.Add(RuleResult.HardStop("ORDER.NUMBER.REQUIRED", "Order number is required."));
        }

        if (order.OrderedUtc == default)
        {
            results.Add(RuleResult.HardStop("ORDER.DATETIME.REQUIRED", "Order date/time is required."));
        }

        if (activeLines.Count == 0)
        {
            results.Add(RuleResult.HardStop("ORDER.LINES.REQUIRED", "At least one test or product is required."));
        }

        foreach (var line in activeLines)
        {
            if (line.LineCategory == OrderCategory.Test)
            {
                if (string.IsNullOrWhiteSpace(line.TestCode) && line.OrderType == OrderType.Other)
                {
                    results.Add(RuleResult.HardStop("ORDER.TEST.REQUIRED", "Test lines must identify the test requested."));
                }
            }
            else if (line.LineCategory == OrderCategory.Product)
            {
                if (line.ProductTypeId is null or <= 0)
                {
                    results.Add(RuleResult.HardStop("ORDER.PRODUCT.REQUIRED", "Product lines must identify the product type requested."));
                }
            }
        }

        if (order.Status == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(order.CancellationReason))
        {
            results.Add(RuleResult.HardStop("ORDER.CANCEL.REASON.REQUIRED", "Cancelled orders require a cancellation reason."));
        }

        return new RuleEvaluation(results);
    }

    public static bool IsEditable(Order order) =>
        order.Status is not (OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued);

    public static RuleResult? ValidateEditable(Order order)
    {
        if (IsEditable(order))
        {
            return null;
        }

        return RuleResult.HardStop("ORDER.EDIT.NOTALLOWED", $"Orders in {order.Status} status cannot be edited.");
    }
}
