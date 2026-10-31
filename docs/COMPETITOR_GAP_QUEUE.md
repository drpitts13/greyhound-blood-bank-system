# Competitor gap queue

Compared with public descriptions of Haemonetics SafeTrace Tx, SCC SoftBank.web, and WellSky Transfusion. This product is compliance-supporting. It is not FDA-cleared, AABB-accredited, or CAP-certified. Do not copy those products' screens, text, or scoring algorithms.

Each open item is one implementation slice. When this list has no open items, stop the competitor-gap loop and do not arm another wake.

## Open

Round 2 (2026-10-31). Scope is the transfusion service only, by product-owner decision. Work one item per loop tick, top to bottom. Each item needs automated tests, a full `dotnet test` run, a TEST-BB ID, an iteration-log entry, and a traceability row.

3. **G3 Coded discard reasons and a wastage report.** Discard reasons are free text today. Plan: add a versioned discard-reason catalog with an admin page, require a code on discard (free text stays optional), and seed a wastage report by product, reason, and month.
4. **G4 Utilization report.** Plan: seed a crossmatch-to-transfusion ratio report by ordering location and provider, plus the share of issued units that were transfused.
5. **G5 Alert inbox.** Today the dashboard shows counts only. Plan: add alerts with acknowledgement. Sources are the expiration sweep, temperature excursions, par shortages, near-expiry units, and overdue reagent QC. In-app only.
6. **G6 CSV export.** Reports export to PDF and XLSX only. Plan: add CSV export to reports and the audit trail. Quote fields per RFC 4180 and neutralize formula injection.
7. **G7 Supplier order from par shortfall.** Plan: generate an editable order from the par board, sign it to submit, export it as PDF or CSV, and link a received delivery to its order. No EDI.
8. **G8 Backup and restore check.** Plan: add backup and verify scripts (SQL Server `WITH CHECKSUM` and `RESTORE VERIFYONLY`, or a SQLite copy), a backup log, and an admin last-backup view.
9. **G9 MFA for privileged users.** Plan: add TOTP for users with admin or supervisor permissions behind a policy that defaults off. The secret is protected with ASP.NET Data Protection.

## Done

1. Computer-evaluated time out of storage. `InventoryLocation.TimeOutGoverned` defaults to governed. Return uses `ReturnReissueRule.TimeOutAcceptable`. A caller flag cannot override the computer. TEST-BB-279.
2. Modification label check. Release of a modification result requires a matching unit-number scan. Other quarantine releases are unchanged. TEST-BB-280.
3. Reagent lot and daily QC. Lots, daily QC, a distinct reviewer, and a worklist. Patient resulting stays open unless policy is turned on. That policy defaults off. TEST-BB-281.
4. Overdue final disposition. Warning board, default 24 hours, no block on issue or documentation. TEST-BB-282.
5. Supply lot on a modification. Optional description and lot, audited and shown on history. Require-lot policy defaults off. TEST-BB-283.
6. Batch modification. One rule and one reason. An ineligible unit modifies none. TEST-BB-284.
7. Par-level stock board. On-hand versus a minimum by product and optional ABO. No blood-center order. TEST-BB-285.
8. G1 Scheduled unit expiration. Hosted sweep every `Inventory:ExpirationSweepSeconds` (default 300) as the system account. The manual sweep keeps its permission check. TEST-BB-295.
9. G2 Storage temperature log. Readings against the location range; an excursion opens a Major deviation and a dashboard count. No automatic quarantine (OCD-036). TEST-BB-296.

## Excluded

These differences stay recorded and are not built in this loop.

- Licensed ISBT / ICCBBA tables (OCD-004). The import path already exists. Do not invent codes.
- RhIG candidacy. Do not invent indications.
- BloodTrack-style device protocol and FHIR. Do not invent a device or FHIR contract.
- Multi-facility consolidated database. That would repartition every query and is not one slice.
- A numeric antigen-match grade. Compatible units are already ranked. A score would copy a patented approach.
- Donor and collection modules (SafeTrace donor, SoftDonor, WellSky Donor). Product-owner decision of 2026-10-31: this product stays a transfusion service.
- Infectious-disease result entry on units. Units arrive tested by the supplier. That entry belongs to the donor side.
- Email and SMS delivery of alerts. No messaging contract is defined. Alerts stay in-app (G5).
- Ordering-appropriateness or patient-blood-management criteria. The thresholds are clinical and REQUIRE REGULATORY / SME VERIFICATION. Do not invent them.
