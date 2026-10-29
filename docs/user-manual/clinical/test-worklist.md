---
slug: test-worklist
title: Test worklist
order: 150
chapter: Testing
summary: Work pending specimens and results in priority order, and verify, correct, or hold them under the result rules.
routes:
  - /test-worklist
---

![Test worklist](figures/test-worklist.png)

The **Test Worklist** is the bench queue for specimens and results. It opens when you have permission to enter results. Rows are ordered so STAT and ASAP work is visible, and blocked rows stay visible with the reason rather than disappearing.

## Specimens on the worklist

The specimen side lists collected specimens waiting to be received and received specimens waiting to be accepted. Receive, then accept, before you result. Reject from here when the specimen cannot be tested. The patient's chart updates with the same status.

## Enter a result

Open the row, enter the values the test definition asks for, and save. Entry and verification are separate steps. You can enter a result you cannot verify. Another qualified person verifies it.

For patient ABO and Rh, the person who entered the result cannot verify it.

> Hard stop. Verify is refused when the specimen is rejected or expired, when the order is on hold, or when you are verifying your own patient ABO or Rh entry.

## Discrepancies

If the ABO or Rh does not match history, verification stops. A supervisor chooses **Retain** (keep the historical type and do not replace it with this result) or **Replace** (accept the new type), with an electronic signature. Do not verify around the delta by editing history.

A manual ABO or Rh override, when policy allows one, is an immunohematology override. It also requires the override permission and a signature. It is not the same action as ordinary verification.

## Interface and retype rows

Results that arrived from an instrument wait for the same verification. The row marks them so you know you did not type them. Read the values.

The product retype tab of the worklist lists units that still need a front type. Recording the retype is the same action as the retype board in Inventory. A match can release the unit. A mismatch quarantines it. An outdated unit that matches is expired rather than made available.

## Correct or invalidate

After verification, a wrong result is corrected or invalidated. Correction requires a signature. Invalidation keeps the prior result visible and removes it from current history. Both are audited. The supervisor queue shows verifications that are waiting when your role is the one who verifies.
