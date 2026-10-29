---
slug: auto-thaw-products
title: Auto thaw products
order: 370
chapter: Inventory and ISBT
summary: List ISBT plasma codes that may be thawed in place when they are allocated. Expiration becomes 24 hours from the thaw.
routes:
  - /admin/auto-thaw-products
---

![Auto thaw products](figures/admin-auto-thaw-products.png)

**Auto thaw products** lists ISBT plasma codes, such as E0707, that may be thawed in place when a technologist allocates them. The unit's expiration becomes 24 hours from the thaw time. Codes that are not on this list are not thawed automatically. They wait for an explicit modification.

Add a code only if your procedure allows thaw-on-allocation for that product and the 24-hour expiration is the dating you intend. A code added by mistake will thaw a frozen unit the moment someone reserves it, and the original frozen expiration will no longer be the one on the label.

Remove a code to stop future auto-thaw. Units already thawed keep the thawed expiration. They do not revert to frozen stock.
