---
slug: inventory-locations
title: Inventory locations
order: 340
chapter: Inventory and ISBT
summary: Define storage sites, satellite refrigerators, and remote-issue locations, including storage-class rules.
routes:
  - /admin/inventory-locations
---

![Inventory locations](figures/admin-inventory-locations.png)

**Inventory Locations** are refrigerators, freezers, platelet incubators, satellite storage, and remote or electronic-issue sites. The unit chart and the inventory list show the location name. The issue gate also reads the location's storage class when it decides whether a unit may be issued from that site.

Create a location before you transfer a unit there. A transfer to an unknown place is not a location. Mark remote-issue locations explicitly so electronic or remote issue rules apply only where you intended.

**Time-out governed** is on for a new location. A governed device uses the facility return-to-available window, which ships at 30 minutes. Clear the box only for a storage device the facility has exempted from that window. An exempt device does not fail the window. The return desk still checks temperature, seal, and appearance. This setting is facility policy. It is not an FDA, AABB, or CAP determination.

Editing, activating, or inactivating a location asks for the reason for the change (at least 8 characters), because these settings are enforced at transfer and issue; the reason is kept in configuration history. Adding a new location does not ask.

Inactivate a location that is out of service so new transfers cannot select it. A location that still holds units cannot be inactivated: transfer them first. Admin › Refrigerators & Freezers is a shorter form for temperature-controlled devices.

## Refrigerator defaults

The storage defaults for a refrigerator include plasma, since thawed plasma is kept refrigerated. Existing locations keep their settings; tick **Plasma** on a refrigerator that holds thawed plasma.
