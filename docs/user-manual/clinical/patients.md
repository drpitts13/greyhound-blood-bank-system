---
slug: patients
title: Finding and registering patients
order: 50
chapter: Patients and orders
summary: Search by MRN or name, open the chart, and register a patient when you have permission to write demographics.
routes:
  - /patients/duplicates
  - /patients
---

![Patients](figures/patients.png)

**Patients** is the directory. Search with the medical record number or the name. Historical names remain searchable after a legal name change, so a former name can still find the chart.

Each row shows the patient's **Location**: the type and place of the current open visit (for example "Inpatient · ICU 4"), the same visit the chart header shows. **+1 more open** in amber means another visit is open too; check which one the work belongs to. **No open visit** means none is open. Use it to tell similar names apart and to confirm where blood is going.

Each row also shows the patient's current **ABO/Rh** and **Alerts**: active antibodies in red, the number of special requirements in effect in amber, and informational notes in blue (notes are shown, never enforced). Use them to confirm you have the right patient and to see their risks before you open the chart; the chart has the details. The patient pickers on the Issue desk, Compatibility, and unit receipt show where the patient is, then the same type and alerts, after the name and MRN; a record merged into another chart says so.

Open a row to go to the chart. The chart is the place for visits, orders, specimens, results, and products. Do not create a second chart because the first search missed a former name.

## Register a patient

**Register patient** is available when your session includes patient write permission. Enter the MRN, date of birth, last name, first name, and sex. Middle name and a comment are optional. Coded comments for the patient page can fill the comment from the catalog.

Once the names and date of birth are entered, the form shows any active patient who may be the same person under another MRN: the same date of birth with the same last name and first initial, or with first and last names swapped. If it is them, **Open chart** and use that record. A second chart splits the patient's ABO/Rh and antibody history. If it is a different person, tick **I checked: this is a different person**; **Register** stays unavailable until you do, and the registration records which charts you checked.

## Possible duplicates

With the patient merge permission, **Possible duplicates** on Patients lists pairs of active charts that may be one person by the same match, including patients registered by the EHR feed. Each chart shows its ABO/Rh, active antibodies, and special requirements, so you can see which one holds the blood bank history. **Keep this, merge … in** opens that chart with its merge form naming the other MRN; give the reason and confirm as for any merge. Charts that are different people stay on the list.

The MRN is the primary identifier you will type at issue and at transfusion documentation. Display names are not identifiers. Two patients must not share an MRN.

> Hard stop. You cannot issue, result, or document transfusion against the losing chart of a merged patient. Search brings you to the surviving chart. If the search still opens an inactive merged record, stop and use the surviving MRN.

## What you cannot do here

Merging two charts is a supervisor action on the chart, not on this list. Correcting a name after registration is done on the chart and asks for a reason. See [The patient chart](patient-chart).
