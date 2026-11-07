---
slug: finding-records
title: Finding a record from the top bar
order: 32
chapter: Getting started
summary: Scan or type a unit number, accession, order number, MRN, visit number, or name in the top bar to open the record.
routes: []
---

The **find box** in the top bar is on every page. Click it, or press **/** or **Ctrl+K**, then scan a barcode or type and press **Enter**. Press **?** outside a text field to see every keyboard shortcut.

It takes:

- **A unit number (DIN)**: scanned from the bag (`=` barcode) or typed, with or without spaces. Opens the unit.
- **A specimen accession or tube barcode**: opens the patient chart on **Specimens** with that specimen's history open.
- **An order number**: opens the patient chart on **Orders** with that order found.
- **An MRN**: opens the chart. A prior MRN opens the current chart and says it matched a prior MRN. An account number or other identifier recorded for the patient works the same way and says which it matched.
- **A visit or account number** (from a requisition or wristband): opens the chart on that visit's orders. The row says the visit's type and location and whether it is still **open**, or when it was **discharged**, so an old requisition is not mistaken for the current visit.
- **Part of a name or number**: lists the matches.

**When it opens by itself.** The record opens directly only when what you entered matches **exactly one** record **exactly**. Anything else shows a list, and you choose:

- Several records match exactly. For example, a DIN whose unit was divided into several components, or one accession and one order with the same number.
- The match is partial, such as part of a name. A single partial match is still listed, not opened, so you confirm it is the right patient.

Use the arrow keys and **Enter**, or click, to open a row. **Esc** closes the list.

**Merged patients.** An MRN that was merged into another chart opens the surviving chart. The row says which MRN it was merged into.

**Patient rows** say where the patient is now ("Now Inpatient · ICU 4", with "+1 more open" when another visit is open), unless the row is for that same visit. They also show the current ABO/Rh (or **No type on file**), active antibodies in red, the number of special requirements in effect in amber, and informational notes in blue, for the chart the row opens, so you can tell similar names apart and see the risks before you open the chart.

**A bad scan.** A scan that starts with `=` but is not a readable DIN says so. It does not fall back to a name search. Scan again.

The find box only opens records. Opening a patient chart records the access in the audit trail, the same as opening it from **Patients**. The search itself is not audited (REQUIRES REGULATORY / SME VERIFICATION if your facility audits searches).

Partial lists show up to 20 patients and 20 units. Type more to narrow the list.
