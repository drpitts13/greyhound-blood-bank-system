# Competitor gap queue

Compared with public descriptions of Haemonetics SafeTrace Tx, SCC SoftBank.web, and WellSky Transfusion. This product is compliance-supporting. It is not FDA-cleared, AABB-accredited, or CAP-certified. Do not copy those products' screens, text, or scoring algorithms.

Each open item is one implementation slice. When this list has no open items, stop the competitor-gap loop and do not arm another wake.

## Open

Round 2 (2026-10-31). Scope is the transfusion service only, by product-owner decision. Work one item per loop tick, top to bottom. Each item needs automated tests, a full `dotnet test` run, a TEST-BB ID, an iteration-log entry, and a traceability row.

None. Round 2 is complete. Stop the competitor-gap loop and do not arm another wake.

## Done

1. Computer-evaluated time out of storage. `InventoryLocation.TimeOutGoverned` defaults to governed. Return uses `ReturnReissueRule.TimeOutAcceptable`. A caller flag cannot override the computer. TEST-BB-279.
2. Modification label check. Release of a modification result requires a matching unit-number scan. Other quarantine releases are unchanged. TEST-BB-280.
3. Reagent lot and daily QC. Lots, daily QC, a distinct reviewer, and a worklist. Patient resulting stays open unless policy is turned on. That policy defaults off. TEST-BB-281.
4. Overdue final disposition. Warning board, default 24 hours, no block on issue or documentation. TEST-BB-282.
5. Supply lot on a modification. Optional description and lot, audited and shown on history. Require-lot policy defaults off. TEST-BB-283.
6. Batch modification. One rule and one reason. An ineligible unit modifies none. TEST-BB-284.
7. Minimum stock board. On-hand versus a minimum by product and optional ABO. No blood-center order. TEST-BB-285.
8. G1 Scheduled unit expiration. Hosted sweep every `Inventory:ExpirationSweepSeconds` (default 300) as the system account. The manual sweep keeps its permission check. TEST-BB-295.
9. G2 Storage temperature log. Readings against the location range; an excursion opens a Major deviation and a dashboard count. No automatic quarantine (OCD-036). TEST-BB-296.
10. G3 Coded discard reasons. Admin catalog, code required on discard (comment when the code says so), and a wastage report by month, product, and code. TEST-BB-297.
11. G4 Utilization. C:T ratio and transfused share by ordering location and provider on `/utilization`; numbers only, no benchmark. TEST-BB-298.
12. G5 Alert inbox. In-app alerts with a top-bar count; excursion and expiry events, plus minimum stock, near-expiry, and reagent QC conditions that resolve themselves. TEST-BB-299.
13. G6 CSV export. Reports and the audit trail export CSV with RFC 4180 quoting and a formula-injection guard; audit exports are themselves audited. TEST-BB-300.
14. G7 Supplier order. Draft from the minimum stock shortfall, e-signed submit, PDF/CSV export, delivery link from the same supplier; no EDI. TEST-BB-301.
15. G8 Database backup. Scheduled and on-demand verified backups (SQLite integrity check, SQL Server VERIFYONLY), audit manifest for restore, retention, failure and overdue alerts. TEST-BB-302.
16. G9 MFA. RFC 6238 authenticator-app second factor; policy (default off) asks privileged users to enroll; key protected with Data Protection; admin reset. TEST-BB-303.
17. OCD-036 / OCD-037 decisions applied: automatic quarantine after a sustained storage excursion; MFA as a facility option with refused sign-in until enrollment. TEST-BB-304, TEST-BB-305.
18. PBM ordering criteria. Facility-entered transfusion indications (none shipped), optional reason for orders outside criteria, review list (OCD-038). TEST-BB-306.

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
- Shipped PBM thresholds. The criteria engine exists (item 18), but its content is clinical and REQUIRES REGULATORY / SME VERIFICATION (OCD-038). Do not invent thresholds.
