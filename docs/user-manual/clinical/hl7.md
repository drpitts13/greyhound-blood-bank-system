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

Every error that closes keeps a note of why: the reason you gave on **Resolve**, a replay that applied, or a later send the receiver acknowledged. **Recently resolved** under the error queue lists the latest ones with who closed them and when.

Outbound messages, including billing DFT, result, and blood product dispense (BPS) messages your interfaces send, appear with their acknowledgement status. A message that has not been acknowledged is still pending. The downtime snapshot counts those so you can see the backlog after an outage.

## What the bench should do

If an order you expected is missing, check this log before you place a duplicate. If the message failed, fix or replay it. If it succeeded, the order is on the chart and you may be searching the wrong MRN. Placing a second manual order for a message that already succeeded creates two orders for one request.

## Blood product dispense to the EHR

When an outbound blood product (BPAM) interface is enabled, every issue and every return sends the EHR a BPS^O29 message. It names the patient, the order the unit was issued against, the DIN, the product code, the ABO/Rh, and the status: DS when the unit leaves for the patient's location, RA when it comes back unused. Bedside administration in the EHR can then match the unit it scans to the issue. The status codes follow HL7 table 0510. If your EHR expects other codes, translate them under the data item `BloodProduct.DispenseStatus`. If a message cannot be queued, the issue or return still stands and the desk shows a warning; resend from this page.
