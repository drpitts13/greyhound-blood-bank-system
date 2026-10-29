# Competitor gap queue

Compared with public descriptions of Haemonetics SafeTrace Tx, SCC SoftBank.web, and WellSky Transfusion. This product is compliance-supporting. It is not FDA-cleared, AABB-accredited, or CAP-certified. Do not copy those products' screens, text, or scoring algorithms.

Each open item is one implementation slice. When this list has no open items, stop the competitor-gap loop and do not arm another wake.

## Open

None. Stop the competitor-gap loop and do not arm another wake.

## Done

1. Computer-evaluated time out of storage. `InventoryLocation.TimeOutGoverned` defaults to governed. Return uses `ReturnReissueRule.TimeOutAcceptable`. A caller flag cannot override the computer. TEST-BB-279.
2. Modification label check. Release of a modification result requires a matching unit-number scan. Other quarantine releases are unchanged. TEST-BB-280.
3. Reagent lot and daily QC. Lots, daily QC, a distinct reviewer, and a worklist. Patient resulting stays open unless policy is turned on. That policy defaults off. TEST-BB-281.
4. Overdue final disposition. Warning board, default 24 hours, no block on issue or documentation. TEST-BB-282.
5. Supply lot on a modification. Optional description and lot, audited and shown on history. Require-lot policy defaults off. TEST-BB-283.
6. Batch modification. One rule and one reason. An ineligible unit modifies none. TEST-BB-284.
7. Par-level stock board. On-hand versus a minimum by product and optional ABO. No blood-center order. TEST-BB-285.

## Excluded

These differences stay recorded and are not built in this loop.

- Licensed ISBT / ICCBBA tables (OCD-004). The import path already exists. Do not invent codes.
- RhIG candidacy. Do not invent indications.
- BloodTrack-style device protocol and FHIR. Do not invent a device or FHIR contract.
- Multi-facility consolidated database. That would repartition every query and is not one slice.
- A numeric antigen-match grade. Compatible units are already ranked. A score would copy a patented approach.
