# Downtime and business continuity

Status: design for the current prototype. This is an operational plan, not a
claim that production disaster-recovery has been validated.

## 1. Goals

- Keep transfusion possible during LIS unavailability using paper/manual
  procedures defined by the facility.
- Prevent silent loss or double-application of orders, results, issues, and
  interface messages after recovery.
- Reconcile identifiers (patient, specimen, DIN/product) before resuming
  electronic issue.

## 2. Modes

| Mode | Trigger | Application behavior (current / intended) |
|---|---|---|
| Planned downtime | Maintenance window | Stop inbound interfaces first; drain outbound queues; switch users to downtime forms. |
| Unplanned outage | Process/host/database failure | Users follow facility downtime SOP. Queued HL7 remains in `Hl7Messages` / `InterfaceErrorQueue` when the database is still reachable. |
| Degraded interface | LIS up, HIS/instrument down | Messages stay in pending/error/retry/manual-review queues. Errors are not discarded. |
| Local SQLite desktop | Development / single-workstation demo | Schema is aligned by `DevelopmentSqliteBootstrap`. Not a validated HA topology. |

## 3. Downtime workflow (facility SOP)

REQUIRES REGULATORY / SME VERIFICATION — the facility must publish the official
downtime SOP. The software does not replace that SOP.

Recommended paper capture (minimum):

1. Two independent patient identifiers.
2. Specimen accession, collection date/time, collector.
3. ABO/Rh, antibody screen, historical antibodies, special requirements.
4. Unit DIN, product code, ABO/Rh, expiration, visual inspection.
5. Compatibility method and result, or emergency-release acknowledgments.
6. Issue, transfusion start/stop, disposition, reaction if any.

## 4. Recovery and reconciliation

1. Restore the database from the last verified backup. Confirm audit-table
   row counts against the backup manifest.
2. Replay interface queues: pending → processed; error → manual review.
   Idempotency keys (`MessageControlId` + business key) must reject duplicates.
3. Enter downtime paper records. Do not reuse an invalid specimen merely
   because a prior result exists.
4. Re-verify patient, specimen, and unit identifiers before the first
   electronic issue after recovery.
5. Complete retrospective crossmatch for any emergency issues recorded on paper.
6. Quality review of overrides, emergency releases, and interface errors.

## 5. Duplicate prevention

| Record | Control |
|---|---|
| Patient MRN | Unique index |
| Visit number | Unique index |
| Specimen accession | Unique index |
| Unit number / component identity | Unique indexes |
| Active allocation | Filtered unique index `IX_Allocations_OneReservedPerUnit` |
| Open issue | Filtered unique index `IX_Issues_OneOpenIssuePerUnit` |
| Billing event | Unique `DedupeKey` |
| HL7 replay | Control id + business key |

## 6. Post-downtime verification checklist

- [ ] Database restore verified; no audit gaps vs backup.
- [ ] Interface inbound/outbound queues drained or parked in manual review.
- [ ] Downtime paper issues entered and units not double-issued.
- [ ] Historical antibodies and special requirements still present.
- [ ] Electronic XM eligibility re-assessed (history still blocks).
- [ ] Emergency-release retrospective XM worklist cleared or assigned.
- [ ] Medical director / supervisor sign-off recorded.

## 7. What this prototype does not yet provide

- Automatic multi-site failover or a validated warm-standby.
- An offline-first mobile downtime client.
- Automated paper-form OCR import.

Those are future enhancements and must be validated before clinical use.
