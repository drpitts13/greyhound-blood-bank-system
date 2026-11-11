using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege and reason gates for creating, withdrawing, or restoring an
/// antibody panel lot. This does not identify antibodies.
/// </summary>
public static class AntibodyPanelLotAdminRule
{
    public const string ActivatePermCode = "ABID-LOT-ACT-PERM";
    public const string DeactivatePermCode = "ABID-LOT-DEACT-PERM";
    public const string DeactivateReasonCode = "ABID-LOT-DEACT-REASON";
    public const string CreatePermCode = "ABID-LOT-CREATE-PERM";
    public const string ManufacturerCode = "ABID-LOT-CREATE-MFG";
    public const string IdentityCode = "ABID-LOT-CREATE-ID";
    public const string ExpiredCode = "ABID-LOT-CREATE-EXPIRED";
    public const string DuplicateCode = "ABID-LOT-CREATE-DUP";
    public const string CellsCode = "ABID-LOT-CREATE-CELLS";
    public const string AntigenCode = "ABID-LOT-CREATE-AG";
    public const string CorrectPermCode = "ABID-LOT-CORRECT-PERM";
    public const string CorrectInUseCode = "ABID-LOT-CORRECT-INUSE";
    public const string CorrectReasonCode = "ABID-LOT-CORRECT-REASON";

    public static RuleResult EvaluateCorrect(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CorrectPermCode)
            : RuleResult.HardStop(
                CorrectPermCode,
                "Correcting an antibody panel lot's antigram requires the admin.config.edit permission.");

    /// <summary>
    /// A lot's antigram may be corrected only before any workup uses the lot: a workup's reactions were read against
    /// the cells as they stood, so once one does, the lot is withdrawn and a corrected lot created instead. A reason is
    /// required, and the corrected cells pass the same checks as a new lot's.
    /// </summary>
    public static IReadOnlyList<RuleResult> EvaluateCorrectDraft(
        int workupsUsingLot,
        string? reason,
        bool isSelectedCellLot,
        IReadOnlyList<AntibodyPanelLotCreateCellDraft> cells,
        IReadOnlySet<long> activeAttributeIds)
    {
        var stops = new List<RuleResult>();
        if (workupsUsingLot > 0)
        {
            stops.Add(RuleResult.HardStop(
                CorrectInUseCode,
                $"{workupsUsingLot} workup{(workupsUsingLot == 1 ? " uses" : "s use")} this lot, so its antigram cannot be changed: " +
                "withdraw the lot and create a corrected one (a new lot number or a suffix), then review the workups that used it."));
            return stops;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            stops.Add(RuleResult.HardStop(
                CorrectReasonCode,
                "Correcting a lot's antigram requires a reason (for example, the manufacturer's corrected sheet, or a typing entered wrongly)."));
        }

        AddCellStops(cells, isSelectedCellLot, activeAttributeIds, stops, "A corrected antigram");
        return stops;
    }

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreatePermCode)
            : RuleResult.HardStop(
                CreatePermCode,
                "Creating an antibody panel lot requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivatePermCode)
            : RuleResult.HardStop(
                ActivatePermCode,
                "Activating an antibody panel lot requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivatePermCode)
            : RuleResult.HardStop(
                DeactivatePermCode,
                "Deactivating an antibody panel lot requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivateReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? RuleResult.HardStop(
                DeactivateReasonCode,
                "Deactivating an antibody panel lot requires a reason. Withdrawal does not identify or void an open workup.")
            : RuleResult.Pass(DeactivateReasonCode);

    public static IReadOnlyList<RuleResult> EvaluateCreateDraft(AntibodyPanelLotCreateDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var stops = new List<RuleResult>();

        if (!draft.ManufacturerIsActive)
        {
            stops.Add(RuleResult.HardStop(
                ManufacturerCode,
                "An active antibody panel manufacturer is required. Creating a lot does not identify antibodies."));
        }

        if (string.IsNullOrWhiteSpace(draft.LotNumber) || string.IsNullOrWhiteSpace(draft.PanelName))
        {
            stops.Add(RuleResult.HardStop(
                IdentityCode,
                "Lot number and panel name are required. Creating a lot does not identify antibodies."));
        }

        if (draft.ExpiresOn < draft.Today)
        {
            stops.Add(RuleResult.HardStop(
                ExpiredCode,
                "A new antibody panel lot cannot be created after its expiration date."));
        }

        if (draft.DuplicateLotNumber)
        {
            stops.Add(RuleResult.HardStop(
                DuplicateCode,
                "That manufacturer already has this lot number."));
        }

        AddCellStops(draft.Cells, draft.IsSelectedCellLot, draft.ActiveAttributeIds, stops, "A new lot");
        return stops;
    }

    private static void AddCellStops(
        IReadOnlyList<AntibodyPanelLotCreateCellDraft>? cellDrafts,
        bool isSelectedCellLot,
        IReadOnlySet<long> activeAttributeIds,
        List<RuleResult> stops,
        string subject)
    {
        var cells = cellDrafts ?? [];
        if (cells.Count == 0)
        {
            stops.Add(RuleResult.HardStop(
                CellsCode,
                $"{subject} requires at least one cell. A blank panel cannot be used for antibody identification."));
            return;
        }

        var numbers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cell in cells)
        {
            var number = cell.CellNumber?.Trim() ?? "";
            if (number.Length == 0)
            {
                stops.Add(RuleResult.HardStop(
                    CellsCode,
                    "Each cell requires a cell number."));
                continue;
            }

            if (!numbers.Add(number))
            {
                stops.Add(RuleResult.HardStop(
                    CellsCode,
                    $"Cell {number} is duplicated on this lot."));
            }

            var expectedRole = cell.Role == PanelCellRole.Autocontrol
                ? PanelCellRole.Autocontrol
                : isSelectedCellLot ? PanelCellRole.Selected : PanelCellRole.Panel;
            if (cell.Role != expectedRole)
            {
                stops.Add(RuleResult.HardStop(
                    CellsCode,
                    isSelectedCellLot
                        ? "Selected-cell lots may include selected cells and an autocontrol only."
                        : "Primary panels may include panel cells and an autocontrol only."));
            }

            var antigens = cell.Antigens ?? [];
            if (cell.Role == PanelCellRole.Autocontrol)
            {
                if (antigens.Count > 0)
                {
                    stops.Add(RuleResult.HardStop(
                        AntigenCode,
                        "Autocontrol cells cannot carry panel antigen typings."));
                }

                continue;
            }

            var typed = new HashSet<long>();
            var hasTyped = false;
            foreach (var antigen in antigens)
            {
                if (!activeAttributeIds.Contains(antigen.BloodAttributeDefinitionId))
                {
                    stops.Add(RuleResult.HardStop(
                        AntigenCode,
                        "Each antigen typing must use an active blood-attribute catalog code."));
                    continue;
                }

                if (!typed.Add(antigen.BloodAttributeDefinitionId))
                {
                    stops.Add(RuleResult.HardStop(
                        AntigenCode,
                        "A cell cannot repeat the same antigen."));
                }

                if (antigen.Expression != AntigenExpression.NotTested)
                {
                    hasTyped = true;
                }
            }

            if (!hasTyped)
            {
                stops.Add(RuleResult.HardStop(
                    AntigenCode,
                    $"Cell {number} needs at least one typed antigen (not Not tested). Creating a lot does not identify antibodies."));
            }
        }
    }
}

public sealed record AntibodyPanelLotCreateDraft(
    bool ManufacturerIsActive,
    string? LotNumber,
    string? PanelName,
    DateOnly ExpiresOn,
    DateOnly Today,
    bool DuplicateLotNumber,
    bool IsSelectedCellLot,
    IReadOnlyList<AntibodyPanelLotCreateCellDraft> Cells,
    IReadOnlySet<long> ActiveAttributeIds);

public sealed record AntibodyPanelLotCreateCellDraft(
    string? CellNumber,
    PanelCellRole Role,
    IReadOnlyList<AntibodyPanelLotCreateAntigenDraft> Antigens);

public sealed record AntibodyPanelLotCreateAntigenDraft(
    long BloodAttributeDefinitionId,
    AntigenExpression Expression);
