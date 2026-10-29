---
slug: hl7-translations
title: Interface translations
order: 390
chapter: Interfaces and printers
summary: Map incoming coded values, such as location or provider codes, onto the codes this system stores.
routes:
  - /admin/hl7/translations
---

![Interface translations](figures/admin-hl7-translations.png)

**Interface Translation Table** maps a value in an inbound message onto a value in this system. Typical rows translate a location code, a provider id, a specimen type, or a test code. The HL7 setup names which fields use the table.

An untranslated code does not create a new location or provider. The message errors, and the order or result does not file. Add the translation, then replay the message from the HL7 console. Replaying before you add the row files the same error again.

When the sending system changes a code, add the new translation and leave the old one if historical replays still need it. Deleting the old row makes those replays fail.

Do not translate two different incoming codes onto one local patient or location unless they are truly the same place. That is how two wards become indistinguishable on the chart.
