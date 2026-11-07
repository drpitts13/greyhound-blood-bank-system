---
slug: compatibility
title: Compatibility testing
order: 250
chapter: Compatibility and issue
summary: Record a serologic or electronic crossmatch and reserve the unit to the patient.
routes:
  - /compatibility
---

![Compatibility testing](figures/compatibility.png)

**Compatibility Testing** is the desk for crossmatch and reservation when you are not already on the patient's Products tab. Identify the patient by MRN or name and the specimen by accession. The specimen must be current and must belong to that patient.

## Record the crossmatch

Choose the unit and the method. The specimen must not have expired: a crossmatch on an expired specimen is refused with the specimen named, so collect a new specimen. The exception is the retrospective crossmatch after emergency release on the pre-transfusion specimen. When that specimen has expired, the form asks for the exception reason and who authorized it, records the crossmatch, and sends the exception to supervisor review. A specimen collected after the unit was issued is still refused. When a reserved unit's crossmatch has expired, its row on the Products tab reads **Crossmatch expired** and offers **Crossmatch** again.

**Serologic** crossmatch asks for the crossmatch test and the reactions (the phases your test definition includes). Record what you saw. Incompatible is a result you can record. It does not become compatible because the patient is urgent.

**Electronic** crossmatch is available only when all of the following are true:

- Facility policy has electronic crossmatch turned on. It is off until an administrator enables it.
- The patient has a current ABO and Rh and a negative antibody screen that meets the visit, specimen, and test minimums in Crossmatch Settings.
- The patient has no antibody history of any kind. A deactivated antibody and an antibody marked currently undetectable still count.
- No antibody-identification workup is open.

If you opened the screen and someone else posted an antibody before you saved, the electronic crossmatch is refused. Record a serologic crossmatch instead.

> Hard stop. Electronic crossmatch is refused when policy disables it, when any antibody history exists, or when an identification workup is open.

## Reserve the unit

After a compatible crossmatch, allocate the unit to the patient's product order. Allocation warns when the unit is not antigen-negative for a requirement the patient has, and when a red cell has not been retyped. Those warnings are there so you pick a different unit before issue, not so you can click past them without reading them.

When you choose the patient, their special requirements appear above the form: requirements in effect as a warning, informational notes below it. A unit that does not meet a requirement (for example a group A red cell for "Group O red cells only", or a unit that is not irradiated) is refused when you reserve it, the same as on the chart and at issue. Choose a unit that meets them. Once the patient is chosen, the unit list offers only units compatible with their type, special requirements, and antibodies, and says how many there are; **Show all available units** lists the rest (Reserve still checks whichever you pick). If the patient has no current ABO/Rh, every available unit is shown and the reason is given. **Record crossmatch** works the same way: once the patient is chosen, its unit list offers the units already reserved for them and the compatible units in stock, says how many of each, and **Show all units** lists the rest. A retrospective crossmatch opened from the issuing board keeps every unit, because its unit was already issued.

An emergency or massive-transfusion issue that still needs a retrospective crossmatch can open this screen with the patient and unit already filled in. Record the crossmatch against the specimen you actually tested. The retrospective queue on the issue desk clears only when that crossmatch is filed. An incompatible result shows a warning on this screen, raises a critical alert, and opens a critical deviation, because the unit may already be at the bedside or transfused.

Crossmatch from the chart uses the same rules in a dialog. See [Products on the chart](patient-products).
