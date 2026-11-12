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

Each open error says how long it has **Waiting** (hover for when it was queued). An order, result, or admission waiting hours has not reached its destination for that long: work the oldest first. The home screen's HL7 errors card names the oldest wait too ("oldest waiting 5 h").

Open an error and read the reason. A rejected order or result did not update the chart. Replaying a message sends it through processing again. Replay when the failure was transient or when the map has been fixed. Do not replay a message that filed on the wrong patient until the map or the translation is corrected, or you will file it wrong again.

Every error that closes keeps a note of why: the reason you gave on **Resolve**, a replay that applied, or a later send the receiver acknowledged. **Recently resolved** under the error queue lists the latest ones with who closed them and when.

**Waiting to send** lists outbound messages (results to the EHR, charges to billing) not yet acknowledged, oldest first, with why each has not gone: no enabled outbound interface is set up to take it (it sends once one is), the last send failed and it will be retried, its retries are spent (fix the interface, then **Send**), or it is simply queued for the next attempt within a minute. A wait longer than 15 minutes is marked: such a message will not go by itself. None of these is an error, so they do not appear in the error queue. In the message log an outbound message not yet sent reads **Queued**. A charge withheld because it was cancelled before sending is not listed.

Outbound messages, including billing DFT, result, and blood product dispense (BPS) messages your interfaces send, appear with their acknowledgement status. A message that has not been acknowledged is still pending. The downtime snapshot counts those so you can see the backlog after an outage.

## What the bench should do

If an order you expected is missing, check this log before you place a duplicate. If the message failed, fix or replay it. If it succeeded, the order is on the chart and you may be searching the wrong MRN. Placing a second manual order for a message that already succeeded creates two orders for one request.

## Blood product dispense to the EHR

When an outbound blood product (BPAM) interface is enabled, every issue and every return sends the EHR a BPS^O29 message. It names the patient, the order the unit was issued against, the DIN, the product code, the ABO/Rh, and the status: DS when the unit leaves for the patient's location, RA when it comes back unused. Bedside administration in the EHR can then match the unit it scans to the issue. The status codes follow HL7 table 0510. If your EHR expects other codes, translate them under the data item `BloodProduct.DispenseStatus`. If a message cannot be queued, the issue or return still stands and the desk shows a warning; resend from this page.

**Transfusions reported by the EHR.** An inbound transfusion message (BPAM) records the transfusion of a unit the blood bank issued. If the blood bank had the unit recalled, quarantined, or reported damaged or missing after it left, the transfusion is still recorded (it happened), but the unit keeps that status and is marked as reported transfused, and the transfusion waits on the supervisor queue as **Reported transfusion** until a supervisor reviews it (OCD-083). A unit in any other status that is not issued (for example discarded) is not recorded: the message goes to the HL7 error queue saying its status.

## Results to the EHR

With an outbound results interface enabled, a result is queued for the EHR as soon as it is verified (OBX-11 F), and a verified correction (C) or a withdrawal (W) follows automatically. Facility Policies › Interface › **Send verified results to the EHR automatically** turns the first final off; then send it from this page on request. Corrections and withdrawals always go. Without an enabled results interface nothing is queued.

## Orders cancelled by the EHR

A cancel (ORC-1 **CA**) or discontinue (**DC**) from the EHR takes the same checks as cancelling on the chart. An order with units still reserved for it is refused (AE, "Units are reserved for order …") and goes to the error queue: release the units, then cancel the order in the LIS or replay the message. Otherwise the order is cancelled with the EHR's reason (ORC-16, the text or else the code; the field can be changed under the interface's data items as *Order control reason*), and only its test lines without a verified result are cancelled: a verified result stays reported.
