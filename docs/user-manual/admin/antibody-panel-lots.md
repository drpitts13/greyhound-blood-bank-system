---
slug: antibody-panel-lots
title: Antibody panel lots
order: 160
chapter: Catalog
summary: Withdraw or restore antibody-identification panel lots. A withdrawn lot cannot be accepted or completed.
routes:
  - /admin/antibody-panel-lots
---

![Antibody panel lots](figures/admin-antibody-panel-lots.png)

**Antibody Panel Lots** tracks the panel lots the identification workup can use. Withdraw a lot when it is expired, contaminated, or recalled. A withdrawn lot cannot be accepted on a new workup and cannot be completed. Restore a lot only if the withdrawal was a mistake and the lot is still valid to result.

Withdrawing a lot does not identify antibodies and does not close workups that were already completed on that lot. Open workups on a withdrawn lot have to move to a valid lot or be closed without using the withdrawn one.

The lot's antigram is what **Assist** compares. A wrong antigram produces wrong suggestions. Assist still does not identify the antibody. Correct the antigram before the bench uses the lot, and record why (see **Correct an antigram** below).

## Lots to replace

The top of the page (and the Antibody ID worklist) lists any primary panel or selected-cell lot that expires within 7 days, saying whether a later lot of the same kind is entered to follow it, and says when no active, unexpired primary panel lot is left, so the next lot can be ordered and entered in time. A selected-cell lot is only expected once one has been entered. The **Panel lots to replace** home card counts the ones with nothing to follow them. The 7 days are the reagent lots' warning window, not a regulatory rule.

## Enter, import or download an antigram

**By hand.** **New lot** opens a form with one cell typed for every active antigen, all absent; mark what is present, add cells, and **Create**.

**Import.** Manufacturers publish each lot's antigram (usually on their customer site). Download it there, save it as CSV or Excel (.xlsx) in this layout, and choose it under **Import an antigram** in the New lot form:

- a header row whose first cell is **Cell** (title rows above it are skipped);
- an optional **Role** column: Panel, Selected or Autocontrol (blank means Panel, or Selected for a selected-cell lot);
- one column per antigen, named by its code in the antigen catalog. C and c, E and e, K and k are different antigens and must match exactly; other spellings such as *Fy(a)* or *anti-Jka* are accepted when only one antigen fits;
- one row per cell; each value is **+** (present), **0** (absent), **w** (weak), **+het**, **+hom**, or blank for not tested. An autocontrol row has no typings.

The import replaces the cells in the form and says how many cells and antigens it read. A column that names no catalog antigen (a donor number, a special-type note) is listed and left out: check that no antigen you need is among them. If any value cannot be read, a cell number repeats, or a role is unknown, the file is not used and each problem is listed by line; correct the file and import again. Nothing is saved until you fill in the manufacturer, lot number, name and expiry and choose **Create**, with the usual checks; the lot's audit entry names the file it was imported from. Always compare the cells with the manufacturer's printed antigram before you create the lot.

**Download.** **Download blank template** (in the New lot form) gives the layout with every active antigen as a column. On a lot, **Download antigram (CSV)** gives its cells in the same layout, so it can be checked, kept, or imported for a new lot. A spreadsheet may show a present value as *'+*: the apostrophe stops it being read as a formula, and the import reads it as present.

## Correct an antigram

Until a workup uses the lot, **View cells** › **Correct antigram** opens the cell editor filled with the lot's cells. Change a typing, add or remove a cell, or import the manufacturer's corrected sheet, give a **reason**, and choose **Save corrected antigram**. The corrected cells pass the same checks as a new lot's. The cells they replace are kept on record (not deleted) and the audit entry holds the antigram before and after, with the reason and any file imported.

Once any workup (open, completed or voided) uses the lot, the antigram cannot be changed: that workup's reactions were read against the cells as they stood. **Correct antigram** is not offered and the server refuses it. If the antigram is wrong, deactivate the lot with the reason, create a corrected lot (the manufacturer's lot number with a suffix, since a lot number cannot repeat), and review the workups listed on the lot.

The system does not fetch antigrams from manufacturers' sites itself: they sit behind each manufacturer's customer login.
