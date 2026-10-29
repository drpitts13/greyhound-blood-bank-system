---
slug: hl7
title: HL7 interface console
order: 340
chapter: Interfaces and reports
summary: Read the message log, open errors, and replay a failed message when you manage the interface.
routes:
  - /hl7
---

![HL7 interface](figures/hl7.png)

The **HL7 Interface** console is the operational log. Admission, order, and result messages are listed with their status. You need permission to manage HL7 to open it. Configuring the connection, the field map, and the translation table is an administrator task. This screen is for seeing what arrived and what failed.

## Errors

Open an error and read the reason. A rejected order or result did not update the chart. Replaying a message sends it through processing again. Replay when the failure was transient or when the map has been fixed. Do not replay a message that filed on the wrong patient until the map or the translation is corrected, or you will file it wrong again.

Outbound messages, including billing DFT and result messages your interfaces send, appear with their acknowledgement status. A message that has not been acknowledged is still pending. The downtime snapshot counts those so you can see the backlog after an outage.

## What the bench should do

If an order you expected is missing, check this log before you place a duplicate. If the message failed, fix or replay it. If it succeeded, the order is on the chart and you may be searching the wrong MRN. Placing a second manual order for a message that already succeeded creates two orders for one request.
