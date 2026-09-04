# Test plan

Layers:

| Layer | Project | Use |
|---|---|---|
| Domain | `BloodBankLIS.Domain.Tests` | Pure rule positive/negative cases |
| Application | `BloodBankLIS.Application.Tests` | Use-case orchestration with fakes |
| HL7 | `BloodBankLIS.HL7.Tests` | Parse/serialize, ACK/NAK |
| Integration | `BloodBankLIS.Integration.Tests` | EF constraints, workflows, SQLite |
| Safety regression | `tests/safety_regression/` | Permanent patient-safety defects |

Safety-critical rules require both a passing (allowed) case and a failing
(blocked) case. Concurrency tests cover double assign and double issue.

Deterministic time uses `IClock`. Do not use wall-clock assertions.

See also `docs/validation-plan.md` for the original strategy text.
