---
slug: ordering-locations
title: Ordering locations
order: 190
chapter: Catalog
summary: Define wards, operating rooms, the emergency department, and other places orders come from.
routes:
  - /admin/locations
---

![Ordering locations](figures/admin-locations.png)

**Ordering Locations** are the wards, operating rooms, emergency department, and other sources an order or a visit can name. Code is required. Name and department are optional but are what people read, so fill them in.

Issue documentation also names a location. Using the same codes keeps the issue destination and the ordering ward comparable. If the interface sends a location code, it must match a row here or be translated. An unknown code fails the message rather than creating a location automatically.

Inactivate a closed ward. Do not delete it if visits already reference it.
