---
slug: instrument-setup
title: Instrument interface setup
order: 400
chapter: Interfaces and printers
summary: Configure ASTM instrument connections, field maps, translations (test, subtest, value, interpretation, and error codes), and if/then message rules.
routes:
  - /admin/instruments
---

![Instrument interface setup](figures/admin-instruments.png)

**Instrument Interfaces** in Administration is the configuration for each analyzer connection. The status log that shows whether messages are arriving is the operational [Instrument interfaces](/help/clinical/instruments) screen. The [simulator](/help/clinical/instrument-simulator) sends a test message.

On this page you set the connection, the field map, the instrument's translations, and if/then rules that adjust a message before it files. **Help** on this screen lists the instrument catalog fields. Use that panel while you are editing. It stays current with the catalog.

## Translations

Each instrument has its own translations. Choose a field at the top of the **Translations** tab:

- **Result test code**: the assay on a result and the LIS test it posts to (Group → ABORH). When one instrument code maps to several tests, the test ordered on the specimen is used.
- **Subtest code**: a well or cell and the LIS subtest it fills (Anti-D1 → Anti-D). **KEEP** stores it under the instrument's own code (a control the LIS keeps as sent); **IGNORE** drops it.
- **Order test code**: the assay the instrument is asked to run for an LIS test when it queries for a specimen's orders. One code per test.
- **Result value** and **Interpretation**: what the instrument reports and the LIS value (B Pos → B|Positive). Limit a row to one test when the same text means different things on different tests.
- **Error or flag code**: the message's error code or a result flag, and what happens: **Hold** (not posted; it waits in instrument failures for review), **Warn** (posted, with the flag beside the result's source for the verifier), or **Ignore**. Once any error code is listed, a code that is not listed holds the result. Which codes mean what comes from the vendor manual (OCD-066).

The local value box suggests the tests, subtests, or actions the field accepts and says what each is; a value the LIS does not have is refused when you save. **Test a value** shows what the saved translations do with a value. **Import rows** takes rows pasted from a spreadsheet or a CSV file (instrument value, local value, optional test); **Export CSV** downloads the field's rows. Save the interface to keep changes; an enabled or production interface asks for a reason.

## When a code has no translation

A result whose test or subtest code has no translation is **not filed**, and neither is a result for a test that is not ordered on the specimen: nothing is filed against another test's order. The message waits in instrument failures and the code is listed under **Unmapped values** (Interface Setup › Translation table) with the instrument's name. **Add translation** there opens this tab with the row ready. Save it, then replay the message from the operational screen. A test with no order test code is left out of the instrument's order download and listed the same way.

Do not map two instrument codes to the same LIS test unless both codes really are that test. You will verify indistinguishable results.

> Warning. A translation change affects the next message from that instrument, including a live analyzer. Prove the map with the simulator on a training accession first.
