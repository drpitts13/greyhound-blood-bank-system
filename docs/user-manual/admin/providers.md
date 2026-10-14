---
slug: providers
title: Ordering providers
order: 200
chapter: Catalog
summary: Define the clinicians who place orders or attend visits.
routes:
  - /admin/providers
---

![Ordering providers](figures/admin-providers.png)

**Ordering Providers** are the clinicians stored on visits and orders. Provider id and name are required. Specialty and location are optional.

Interface orders match the ordering provider by the id in the message, after translation. A provider missing from this list causes the order message to fail or to arrive without a provider, depending on the map. Add the provider rather than substituting a generic "staff" provider, which makes every order look the same in the audit trail.

Inactivate a provider who has left. Historical orders keep the name that was current when they were placed.
