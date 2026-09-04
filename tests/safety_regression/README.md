# Patient-safety regression suite

Permanent tests for hazards that must never silently return. Each discovered
patient-safety defect adds a test here.

These files compile into `BloodBankLIS.Integration.Tests` (and domain-only
cases may also live in `BloodBankLIS.Domain.Tests`).

| Test | Hazard |
|---|---|
| `IssueGateSafetyRegressionTests` | ABO-incompatible, expired, quarantined/recalled, invalid specimen, unauthorized emergency, autologous mismatch |
| `AllocationIssueConcurrencyTests` | Double reservation / concurrent issue |
| `ElectronicXmHistoryRegressionTests` | Historical / currently undetectable antibody restores electronic XM |
| `MergedPatientClinicalUseTests` | Testing, allocation, or issue against a merged (losing) patient record |
