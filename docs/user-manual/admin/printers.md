---
slug: printers
title: Printers
order: 410
chapter: Interfaces and printers
summary: Register PDF queues and Zebra printers by network, USB, Windows driver, serial, or Bluetooth.
routes:
  - /admin/printers
---

![Printers](figures/admin-printers.png)

**Printers** registers the queues the print screen can send to. PDF queues are the printers installed on this computer for preview and paper. Zebra printers can be network, USB, Windows driver, serial, or Bluetooth. A printer that is not registered here does not appear when a technologist prints a label or a tag.

Test a new printer with a specimen label and a P-tag before you tell the bench to use it. Label stock and tag stock are different sizes. A queue pointed at the wrong device prints a specimen label on a tag or sends ZPL to a PDF driver.

This page does not design the label. Layouts are [Specimen label](specimen-label) and [P-tag layout](ptag). Removing a printer stops new jobs from selecting it. It does not recall labels already on the bench.
