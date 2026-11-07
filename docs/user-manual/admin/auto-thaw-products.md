---
slug: auto-thaw-products
title: Auto thaw products
order: 370
chapter: Inventory and ISBT
summary: List ISBT plasma codes that may be thawed in place when they are allocated, each with its thawed shelf life.
routes:
  - /admin/auto-thaw-products
---

![Auto thaw products](figures/admin-auto-thaw-products.png)

**Auto thaw products** lists ISBT plasma codes, such as E0707, that may be thawed in place when a technologist allocates them. The unit's expiration becomes the code's **thawed shelf life** after the thaw time, never later than the frozen expiration. Set the shelf life when you add a code (it starts at 24 hours) and use **Change** to correct it; a change needs a reason and is kept in the configuration history. Take each value from your SOP and the product's circular of information (OCD-068). Codes that are not on this list are not thawed automatically. They wait for an explicit modification.

Add a code only if your procedure allows thaw-on-allocation for that product and the 24-hour expiration is the dating you intend. A code added by mistake will thaw a frozen unit the moment someone reserves it, and the original frozen expiration will no longer be the one on the label.

Remove a code to stop future auto-thaw. Units already thawed keep the thawed expiration. They do not revert to frozen stock.
