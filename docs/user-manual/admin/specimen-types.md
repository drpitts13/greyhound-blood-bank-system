---
slug: specimen-types
title: Specimen types
order: 90
chapter: Catalog
summary: Define specimen types used at accession, including tests that cannot be performed on that type.
routes:
  - /admin/specimen-types
---

![Specimen types](figures/admin-specimen-types.png)

**Specimen Types** is the list accessioning offers. Each type can exclude tests that must not be ordered on it. The exclusion is enforced when the order is placed or the specimen is linked. It is not a warning the bench is expected to remember.

Add a type only when collection and testing rules differ. A duplicate type with a slightly different name splits the worklist and the interface map. If an interface sends a specimen-type code, that code must match a row here or the message fails translation.

Inactivate a type that should no longer be collected. Historical specimens keep the type they were drawn as.
