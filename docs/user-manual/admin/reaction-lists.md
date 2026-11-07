---
slug: reaction-lists
title: Reactions and treatments
order: 185
chapter: Catalog
summary: Define the reactions a provider can conclude and the treatments for future transfusions, chosen when a reaction investigation closes.
routes:
  - /admin/reaction-lists
---

**Reactions and Treatments** holds the two lists a provider chooses from when closing a reaction investigation (OCD-057). Use the **Reactions** and **Treatments** buttons to switch between them.

- **Reactions** is the reaction the provider concluded. The starter list follows the CDC NHSN Hemovigilance adverse reaction categories as an example to edit.
- **Treatments** is what is done for the patient's future transfusions, for example a product modification or premedication. None are shipped: your medical director defines them.

**No reaction** and **No treatment** are always offered. They cannot be deactivated and their code cannot be changed; you can change how their text reads.

Each entry has a code, the text the provider sees, and a sort position. Codes are unique within a list; NONE is reserved. Deactivating an entry removes it from the choices; investigations already closed keep the text they recorded. Every change is recorded in configuration history.

The investigation cannot be closed until the provider has chosen a reaction (or No reaction) and at least one treatment (or No treatment). No treatment cannot be combined with a treatment.
