---
slug: tests
title: Test definitions
order: 50
chapter: Catalog
summary: Define each test's result type, panel, interpretation, and whether it contributes to blood-type or antibody history.
routes:
  - /admin/tests
---

![Test definitions](figures/admin-tests.png)

**Test Definitions** is the catalog the order entry and the worklist use. A test that is not here cannot be ordered. Each definition says how the result is recorded (a choice list, a graded reaction, free text), which panel subtests it includes, and how an interpretation is derived. The list keeps the test code. When the name is blank, the list says Test and the id. That wording is not saved, and the edit form still shows the stored name.

## Specimen types

**Preferred specimen type** prefills the specimen type when the test is accessioned. **Also accepted** lists the other types the test may be performed on (for example EDTA preferred, clotted also accepted). Result entry is blocked only when the specimen is none of these; order entry warns before that. When you order with **Accession now**, the chart names the types the selected tests accept, marks the types that suit them all, prefills one of those, and says when no single tube does (collect a tube for each). Leave the preferred type empty to accept any type. A specimen type's own **excluded tests** still apply. Which types each test accepts is your laboratory's decision.

## Numeric results

For a **Numeric** test, set the **Units** (for example %), **Decimal places**, and any of three ranges. Each is optional and your facility's own:

- **Reportable**: what the method can report. A value outside it is refused at entry (dilute and repeat, or report it as above or below the range).
- **Reference**: a value outside it is flagged **L** or **H**.
- **Critical**: a value beyond it is flagged **LL** or **HH** and warns when saved.

The reference range must lie inside the reportable range, and the critical limits outside the reference range. The flag is stored with the result and sent to the EHR, with the units and the reference range.

## Titers

For a **Titer** test, set the **First** and **Last dilution** (the last must be the first doubled, for example 1 to 2048), the **phase** the tubes are read at, the **weakest grade that counts** toward the endpoint, and whether a **score** is calculated. The form lists the dilutions. Which grade is the endpoint, how the score is counted, and what change matters are decisions for your medical director (OCD-065).

## LOINC code

**LOINC code** (optional) is the standard code the EHR receives with each result of the test, beside your local test code (for example 882-1). The check digit is checked when you save. Your interface team chooses the code; none is filled in for you.

## Example tests

The catalog includes example tests as **inactive drafts**: antibody and ABO titers, fetal bleed quantitation, a DAT with IgG and C3, an elution, a weak D panel, and an Rh/Kell phenotype. Review each with your medical director, adjust it, and activate only the ones your laboratory performs.

## History

Mark the tests that write patient ABO, Rh, antibody screen, or antibody identification into history. Those are the tests the issue gate and electronic crossmatch read. A test you forget to mark will verify on the worklist and still leave the chart without a blood type. A test you mark by mistake can overwrite history with a value that was never meant to be a type.

## Interpretation

Panel interpretation uses the subtests and the logic table on the definition. Changing the logic changes what "positive" means for every future verification. It does not rewrite verified history. Coordinate the change with the medical director and record the reason.

Inactivating a test stops new orders. It does not remove historical results. Prefer inactive over deleting a code that already appears on patient records.

Billing for a verified test is mapped on [Test and service billing](test-service-billing), not on this page.
