---
slug: hl7-translations
title: Interface translations
order: 390
chapter: Interfaces and printers
summary: Translate test and procedure codes, race, ethnicity, patient class, sex, result flags, and other coded values between this system and the EHR, for all interfaces or one.
routes:
  - /admin/hl7/translations
---

![Interface translations](figures/admin-hl7-translations.png)

**Interface Translation Table** turns a value this system stores into the value an interface sends (outbound), and a value an interface receives into the value this system stores (inbound). A row can work in one direction or both.

## Choosing a field

The fields are listed on the left, grouped as Tests and procedures, Patient, Visit, Orders, Results, Billing, and Blood products, with how many rows each has. Type in **Find a field** to narrow the list, by name or by HL7 field (for example *race* or *PV1-2*). Choose a field to see its rows.

**Applies to** chooses whose rows you see. **All interfaces** rows apply to every interface. Choosing one interface shows that interface's own rows, which win over the all-interfaces rows for the same value and direction; the all-interfaces rows are listed under them for reference.

## Editing rows

**Local value** is what this system stores. Where the field has a fixed list (tests and groupers, sex, visit type, race, ethnicity, abnormal flag), the box suggests the list and says what each value is, and a value not on the list cannot be saved. **External value** is what the EHR sends or expects. Every save asks for a **reason**, which is kept in the configuration history with the rows before and after.

## Importing and exporting rows

**Import rows** takes rows pasted from a spreadsheet, or a CSV file: local value, external value, and direction (Inbound, Outbound, or Both; blank means Both), with or without a header line. Choose whether they are added to the rows shown or replace them, then **Put rows in the table**. A line that cannot be read (a blank value, an unknown direction, extra columns) is listed by line number and left out; rows already in the table are not added twice. Nothing is saved yet: check the rows, give a reason, and **Save**, which checks the local values against the field's list as usual.

**Export CSV** downloads the rows shown, in the same three columns, so the interface team can review them in a spreadsheet and send them back for import.

## Test and procedure codes

**Test or grouper procedure code** is one field for orders in (OBR-4 on ORM) and results out (OBR-4 on ORU), so one row such as **TNS ↔ LAB10117** serves both. Map a code to a test, or to a test grouper:

- **Inbound**, a code that matches both a grouper and a test opens the grouper, and the order gets the grouper's tests. Each test line remembers its grouper.
- **Outbound**, a result whose line came from a grouper is sent under the grouper's procedure code when the grouper has one (an ABORH result from a TNS order goes as LAB10117, with ABORH in OBX-3); otherwise under the test's own code.
- An inbound order code that translates to no active test or grouper is **not filed**. The message goes to the interface error queue and the code is listed under **Unmapped values**. Add the translation, then replay the message from the HL7 console.

## Patient and visit values

Race (PID-10) and ethnicity (PID-22) are stored on the patient only when they translate to a value on this system's list; otherwise the patient keeps what they had and the value is listed as unmapped. Patient class (PV1-2) sets the visit type; a class that does not translate leaves a new visit as *Unknown* and an existing visit unchanged. The starter rows use the HL7 patient classes and the CDC race and ethnicity codes. Confirm them with your EHR team.

## Test a value

**Test a value** shows what an interface would do with a value using the saved rows: the result, which field's row it came from, and whether it was an interface override. For an inbound procedure code it also says whether the code opens a grouper or a test, or would go to the error queue.

## Unmapped values

**Unmapped values** lists every value a message carried that no translation covered, with the field, direction, interface, how many times it was seen, and the last message. Only fields that have translations are listed, so free-text fields such as names do not appear. **Add translation** opens the field with a row ready for the value; saving it clears the entry. **Dismiss** clears an entry that needs no translation and asks why. The home screen card **Unmapped interface values** counts the open entries for interface administrators.

## Good practice

When the sending system changes a code, add the new translation and leave the old one if historical replays still need it. Deleting the old row makes those replays fail.

Do not translate two different incoming codes onto one local location unless they are truly the same place. That is how two wards become indistinguishable on the chart.
