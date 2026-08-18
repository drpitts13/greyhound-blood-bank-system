# ISBT 128 Blood-Component Module

**Status:** Reference implementation inside the existing Blood Bank LIS.  
**Not a claim of regulatory certification or production readiness.**

This module adds a normalization pipeline so scanner and human-readable inputs become one canonical `BloodUnit` (table `BloodProducts`). Downstream workflows use normalized fields and must not re-parse raw barcode strings.

## Architecture

```text
Scanner or manual input
        ↓
Input sanitation
        ↓
ISBT data-structure classification
        ↓
Structure-specific parsers
        ↓
Canonical component model (BloodUnit)
        ↓
Field-level + cross-field validation
        ↓
Compatibility rules engine (table-driven)
        ↓
Inventory status state machine
        ↓
Immutable audit / status history
```

### Key projects

| Layer | Location |
|---|---|
| Domain parsers / identity / validators | `src/BloodBankLIS.Domain/Isbt128/` |
| Application services | `src/BloodBankLIS.Application/Isbt128/` |
| Persistence / seed | EF configs, migration `Phase_Isbt128_ComponentIdentity`, `DatabaseSeeder.SeedIsbt128LookupsAsync` |
| API | `/api/isbt/*` (`IsbtEndpoints`) |
| UI | `/inventory/isbt-receive` |

## Component identity

```text
ComponentIdentity = DIN13 + "|" + ProductCodeData[+ "|" + ExtendedDivision]
```

Example: `G123417654321|E0206000`

- Unique index on `ComponentIdentityKey`
- `UnitNumber` for new ISBT units equals `ComponentIdentity` (legacy search compatibility)
- Never identify a component by DIN alone

## Data model additions

- Extended `BloodProducts` columns for DIN / ABO code / product data / local expiration
- `BloodComponentRawScans`, `BloodComponentSpecialTests`
- `BloodComponentScanSessions` (+ lines)
- `IsbtAboRhdCodes`, `IsbtProductCodes`, `IsbtCollectionTypes`, `IsbtDataStructures`
- `BloodComponentCompatibilityDecisions`, `BloodComponentIdentityCorrections`, `BloodComponentExceptions`
- `CompatibilityRuleVersions`, `CompatibilityRules`
- Expanded `UnitStatus` enum + `InventoryStatusTransition` allow-list

## API

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/isbt/parse` | Sanitize, classify, parse one input |
| POST | `/api/isbt/scan-sessions` | Start accumulator |
| POST | `/api/isbt/scan-sessions/scans` | Add scan(s) |
| POST | `/api/isbt/scan-sessions/complete` | Validate + receive |
| POST | `/api/isbt/manual-entry` | Structured human-readable receive |
| POST | `/api/isbt/identity-corrections` | Controlled identity correction |
| POST | `/api/isbt/units/{id}/recall` | Recall |
| POST | `/api/isbt/units/{id}/quarantine` | Quarantine |

Errors return machine-readable codes (e.g. `ISBT_DIN_CHECK_MISMATCH`, `COMPONENT_DUPLICATE`) with human-readable messages. Stack traces are not exposed.

## UI workflow

1. Open **ISBT Receive** (`/inventory/isbt-receive`).
2. **Manual:** enter the combined donation/unit number (DIN), ABO lookup code, product PDC + collection + division, expiration; submit.
3. **Scanner:** start session; scan or paste DIN, ABO, product, expiration (concatenated payloads supported); complete to receive.
4. Derived encoded values are display-only outside identity-correction workflow.

## Configuration

- Scanner prefixes/suffixes/AIM stripping: `ScannerInputSanitizer.Options`
- Date-only expiration default time: `ExpirationParser.Policy` (default 23:59) — **medical-director approval**
- Facility timezone: stored on unit as `ExpirationTimezone`
- DIN check: `IDinCheckCharacterValidator` (default `Iso7064Mod37_2DinCheckCharacterValidator`; obsolete alias `PlaceholderDinCheckCharacterValidator`)
- Lookup tables: `IsbtProductCodes` is seeded with a commonly published US supplier subset of Product Description Codes (`US-PUBLIC-SUBSET-PENDING-ICCBBA` in `UsSupplierProductCodeSeed`); replace/extend with facility-validated ICCBBA data before clinical use
- Admin UI: **Administration → ISBT Product Codes** (`/admin/isbt-product-codes`) lists the seeded codes (read-only); requires `admin.config.view`
- Legacy inventory receive (`POST /api/inventory/units`) requires a matching PDC (5-char) or product code data (8-char) from `IsbtProductCodes`; unknown/retired codes are rejected with `ISBT_UNKNOWN_PRODUCT_CODE` / `ISBT_RETIRED_PRODUCT_NOT_ALLOWED`

## Testing

```bash
dotnet test tests/BloodBankLIS.Domain.Tests --filter FullyQualifiedName~Isbt128
dotnet test tests/BloodBankLIS.Integration.Tests --filter FullyQualifiedName~Isbt128
dotnet test
```

Coverage includes sanitation, DIN/ABO/product/expiration parsing, identity uniqueness, scan session, and scan verification hard-stops.

## Demo scenarios (placeholder values)

### Manual

```text
Donation / unit number: G123417654321  (or G1234 17 654321 [check])
ABO/RhD code: DEMO  (PLACEHOLDER — not an official ISBT encoding)
Product: E0206 / collection 0 / division 00  (US public subset: irradiated CPDA-1 RBC)
Expiration: future local date/time
```

### Scanner

```text
=G12341765432100
=%DEMO
=<E0206000
&>... (or =>cyyjjj date-only)
```

## Known assumptions

1. Official ICCBBA product and ABO/RhD tables are licensed IP — shipped product rows are a **US public subset** (`IsPlaceholder=true`) pending facility ICCBBA validation; ABO/RhD demo codes remain placeholders.
2. DIN keyboard check uses **ISO/IEC 7064 MOD 37-2** (`Iso7064Mod37_2DinCheckCharacterValidator`) over the 13-character DIN. Official ICCBBA product and ABO/RhD tables remain licensed IP and are not shipped here.
3. Century / ordinal-date mapping follows a conservative interpretation pending ICCBBA confirmation.
4. Date-only labels use institutional end-of-day (23:59) unless policy differs.
5. Legacy units without `ComponentIdentity` keep prior issue/transfusion paths (scan verification optional).
6. `Allocated` remains for back-compat; new assignments prefer `Assigned`.

## ICCBBA confirmation checklist

- [ ] DIN keyboard check-character algorithm and official test vectors
- [ ] ABO/RhD data structure encodings and collection categories
- [ ] Product description codes, collection types, divisions, extended division rules
- [ ] Expiration and collection date/time data identifiers and century rules
- [ ] Additional data structures required by the facility

## Institutional / medical-director approval checklist

- [ ] Date-only expiration operational time policy
- [ ] Quarantine disposition criteria at supplier receipt
- [ ] Historical/retired code acceptance policy
- [ ] Return time/temperature/integrity thresholds
- [ ] Emergency-release authorization and second-approver rules
- [ ] Compatibility rule tables by component class
- [ ] Post-transfusion identity-correction workflow
- [ ] Electronic crossmatch eligibility policy

## Safety notice

This software is safety-critical by design intent but is **not** certified. Do not deploy for clinical use without facility validation, medical-director approval, and confirmation against current ICCBBA documentation.
