# Safety architecture

This document describes how Greyhound Blood Bank LIS prevents an incompatible,
expired, quarantined, recalled, misidentified, or otherwise unsuitable
component from being transfused. It does not claim automatic accreditation.

Canonical rule list: [`docs/safety-rules.md`](safety-rules.md).
Issue aggregation: `BloodBankLIS.Domain.Rules.IssueGate`.

## Severity model

| Severity | Behavior |
|---|---|
| HardStop | Blocked. No override path. |
| Warning | Blocked until reason + authorized override + electronic signature + audit. |
| Pass | No objection. |

The engine never auto-downgrades a HardStop. Any HardStop wins the evaluation.

## Controls by risk

Use the strongest control that matches the hazard:

- Hard stops for identity, ABO incompatibility, expired/quarantined/recalled
  units, merged patients, missing required XM (non-emergency), and missing
  privileges
- Warnings for overridable clinical exceptions (emergency missing XM, some
  antigen-negative and ABID-in-progress notices)
- Acknowledgment for advisory ABID completion leftovers (does not identify)
- Second-user verification (patient ABO/Rh self-verify, discard, quarantine
  release, issue dual ID) when facility policy requires it
- Electronic verification (ISBT scan at issue / ward receipt / bedside; two-token patient identity at issue and transfusion documentation)
- Role-based overrides with reason, authorizer, and signature
- Immutable (append-only) audit in the same database transaction as the change,
  with a SHA-256 hash chain on rows written after Cycle 4 (RISK-BB-257)

Never silently bypass a safety rule.

## Where rules live

```
UI / barcode  →  API permission filter  →  Application service
                                          →  Domain rule (pure)
                                          →  database constraint / unique index
                                          →  named AuditEvent
```

Compatibility and eligibility logic must not be implemented only in Blazor
components, controllers, or database triggers. Order/test reflexes use a
separate configurable `RuleEngineService`. ISBT compatibility citations live
in a versioned catalog and do not replace `IssueGate`.

## Electronic crossmatch

`ElectronicCrossmatchEligibilityService` returns `eligible` plus reasons.
Prerequisites include current ABO/Rh, two concordant determinations, negative
screen, no antibody-history row (including deactivated), no open antibody-
identification workup, and facility policy `AllowElectronicCrossmatch`
(off by default; OCD-006 closed). Historical/undetectable antibody still
blocks (`XM-EC-HISTORY`). **REQUIRES REGULATORY / SME VERIFICATION** (OCD-001).

## Emergency release

Distinct `IssueType`. Missing XM becomes an overridable Warning. Identity,
unit status, autologous/directed, and `issue.emergency-release` remain
HardStops. Retrospective XM worklist follows the issue.

## Bedside identification (Cycle 6)

Documenting a transfusion uses the same `PatientIdentityMatchRule` tokens as issue.
A client checkbox is not positive patient identification. Electronic `TX-DUAL-ID`
is complete only after those tokens match and an ISBT bedside unit scan verifies.
Legacy units without `ComponentIdentity` do not invent a scan requirement.
`RequireSecondVerifier` remains an optional facility policy (OCD-008 closed).

## Identity (Cycle 1)

Interactive HTTP callers authenticate with a server-issued session token
(`Authorization: Bearer`). The API does not treat a self-asserted user name
header as proof of identity unless an explicit test-only flag is on.
Application permission HardStops still run after authentication and re-read
the directory on each request. Role or role-permission changes revoke
outstanding sessions. Development `DevMode` remains a Development-only bypass.

## Explainability

Every automated eligibility or compatibility decision carries a stable rule
code and a human-readable reason, for example:

```
Product rejected:
ISS-ABO-COMPAT
Patient group O; unit group A.
ABO-incompatible red cells.
```

## Red-team stance

Safety-critical paths are tested for wrong patient/specimen/unit, expired or
quarantined or recalled units, ABO incompatibility, historical antibody,
invalid specimen, unauthorized override, double assign/issue, and (Cycle 1)
spoofed operator identity.
