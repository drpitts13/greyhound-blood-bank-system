using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Immunohematology;

/// <summary>
/// Withdraws technologist interpretation and supervisor review when a patient
/// type fact changes while an antibody-identification workup is open. Does not
/// identify antibodies.
/// </summary>
internal static class AntibodyIdentificationJudgmentWithdrawal
{
    public static async Task WithdrawAfterPatientTypeChangeAsync(
        IRepository<AntibodyIdentificationWorkup> workups,
        IRepository<AntibodyIdentificationFinding>? findings,
        IAuditWriter audit,
        IUnitOfWork unitOfWork,
        long patientId,
        string reason,
        CancellationToken ct)
    {
        var open = await workups.ListAsync(
            w => w.PatientId == patientId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);
        foreach (var listed in open)
        {
            var workup = await workups.GetByIdAsync(listed.Id, ct);
            if (workup is null)
            {
                continue;
            }

            var hadJudgment = workup.InterpretedUtc is not null || workup.ReviewedUtc is not null;
            if (hadJudgment && findings is not null)
            {
                var listedFindings = await findings.ListAsync(
                    f => f.WorkupId == workup.Id && f.Source == AntibodyIdSource.Technologist, ct);
                foreach (var listedFinding in listedFindings.Where(f =>
                             f.Rationale != "Superseded by a later technologist interpretation."))
                {
                    var finding = await findings.GetByIdAsync(listedFinding.Id, ct);
                    if (finding is null)
                    {
                        continue;
                    }

                    finding.Rationale = "Superseded by a later technologist interpretation.";
                    findings.Update(finding);
                }

                workup.InterpretedUtc = null;
                workup.SupervisorAccepted = false;
                workup.SupervisorUser = null;
                workup.ReviewedUtc = null;
                workup.SupervisorComment = null;
                if (workup.Status is AntibodyWorkupStatus.PendingSupervisorReview)
                {
                    workup.Status = AntibodyWorkupStatus.PendingInterpretation;
                }
            }
            else if (workup.Status == AntibodyWorkupStatus.InProgress)
            {
                workup.Status = AntibodyWorkupStatus.PendingInterpretation;
            }

            workups.Update(workup);
            audit.Record(
                AuditEventType.Antibody,
                nameof(AntibodyIdentificationWorkup),
                workup.Id,
                newValue: new { TypeChangedAfterJudgment = hadJudgment, workup.Status },
                reason: reason);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public static async Task WithdrawAfterLinkedSpecimenUnusableAsync(
        IRepository<AntibodyIdentificationWorkup> workups,
        IRepository<AntibodyIdentificationFinding>? findings,
        IAuditWriter? audit,
        IUnitOfWork unitOfWork,
        long specimenId,
        string reason,
        CancellationToken ct)
    {
        var open = await workups.ListAsync(
            w => w.SpecimenId == specimenId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);
        foreach (var listed in open)
        {
            var workup = await workups.GetByIdAsync(listed.Id, ct);
            if (workup is null)
            {
                continue;
            }

            var hadJudgment = workup.InterpretedUtc is not null || workup.ReviewedUtc is not null;
            if (hadJudgment && findings is not null)
            {
                var listedFindings = await findings.ListAsync(
                    f => f.WorkupId == workup.Id && f.Source == AntibodyIdSource.Technologist, ct);
                foreach (var listedFinding in listedFindings.Where(f =>
                             f.Rationale != "Superseded by a later technologist interpretation."))
                {
                    var finding = await findings.GetByIdAsync(listedFinding.Id, ct);
                    if (finding is null)
                    {
                        continue;
                    }

                    finding.Rationale = "Superseded by a later technologist interpretation.";
                    findings.Update(finding);
                }

                workup.InterpretedUtc = null;
                workup.SupervisorAccepted = false;
                workup.SupervisorUser = null;
                workup.ReviewedUtc = null;
                workup.SupervisorComment = null;
                if (workup.Status is AntibodyWorkupStatus.PendingSupervisorReview)
                {
                    workup.Status = AntibodyWorkupStatus.PendingInterpretation;
                }
            }

            workups.Update(workup);
            audit?.Record(
                AuditEventType.Antibody,
                nameof(AntibodyIdentificationWorkup),
                workup.Id,
                newValue: new { SpecimenUnusableAfterJudgment = hadJudgment, workup.Status },
                reason: reason);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
