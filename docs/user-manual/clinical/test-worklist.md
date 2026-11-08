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

The **Test Worklist** is the bench queue for specimens and results. It opens when you have permission to enter results. Rows are ordered so STAT and ASAP work is visible, and blocked rows stay visible with the reason rather than disappearing. A result waiting for verification whose specimen expires within the facility near-expiry window comes next, with a **Verify before** time: after the specimen expires that result can no longer be verified. The supervisor queue lists those first among results to verify.

## Specimens on the worklist

The specimen side lists collected specimens waiting to be received and received specimens waiting to be accepted. Receive, then accept, before you result. To receive, scan the tube into **Scan tube to receive**: the scan picks the specimen by its accession or label barcode, so the tube in hand is the one received, and the field clears for the next tube. A tube that is not on the list, or is already received, is refused with its accession named. Typing in the field filters the list; Receive on a row still works, unless your facility has turned on *Receive specimens by scan only* (Facility Policies). Then rows say **Scan to receive** and a receipt from a row is refused, so every tube is received by scanning it. To accept received tubes by scan, choose **Accept** next to the field (the label changes to "Scan tube to accept"); switch back to **Receive** when you are done. Each row shows how long the tube took from draw to receipt, or how long it has been waiting since draw. Reject from here when the specimen cannot be tested. A tube that expired while waiting says **Expired: reject it** in place of Receive or Accept, because it can no longer be received or accepted; reject it and request a new specimen. The patient's chart updates with the same status.

## Enter a result

When your facility blocks patient resulting while reagent QC is open, and daily QC is overdue, failed, or not yet reviewed, a red notice at the top of the worklist says so and lists each open item. **Open Reagent QC** takes you to the QC page. Results cannot be saved until it clears, so deal with the QC first instead of keying results that will be refused.

Open the row, enter the values the test definition asks for, and save. From the keyboard, press **J** for the next test and **K** for the previous one (outside a text field): the row opens with the cursor in its first result field. A key never saves or verifies; that stays a click. Entry and verification are separate steps. You can enter a result you cannot verify. Another qualified person verifies it. A row whose result is already entered and waiting says **Verify**, whether a colleague, an instrument, or the interface entered it; open it and verify the value without re-keying it. The prompt says where the value came from. **To verify** above the list shows only the rows you can verify now; your own ABO/Rh entries are left out because someone else verifies them. **All** shows the whole list again.

When your medical director has turned on batch verification (Facility Policies › *Allow batch verification*), **To verify** also shows **Verify in a batch**: the routine results you may verify together. These are plainly Negative antibody screens and DATs, and ABO/Rh results that match the patient's historical type, entered by someone else, on patients with no antibody history and no special requirement. Untick any you want to look at individually, then press **Verify n selected**. Each result is still checked exactly as a single verification would check it; any that cannot be verified is listed by test and patient with the reason, and stays in the list below. A first-time ABO/Rh, a discrepancy, a positive, a correction, and anything on a patient with antibodies or special requirements are always verified one at a time. **Recollect** appears when lines wait for a new specimen because their tube was rejected or cancelled; it lists just those, each with **Accession**. **No specimen** appears when ordered lines have no tube yet; it lists them STAT and urgent first, then oldest first, so you can call for the draw or accession the tube.

**Scan a tube to work its tests** (its label barcode or its accession number) shows only the tests on the tube in your hand and opens the first one that can take a result; save it and the next test on the same tube opens. **Show all tubes** clears it. When you finish the last test on a tube that has not been put away, a note offers **Store tube**, which opens Store tubes with that tube ready; scan its place and press Enter in Tube. A test added by a reflex rule carries a **Reflex from** badge with the result that added it; when your verify adds one, a note at the top names it. If the result that added it is later corrected or invalidated so it no longer matches, the badge turns amber and the row asks you to check whether the test is still needed. Transfusion reaction workup tests are Stat orders on the pre- and post-transfusion tubes, marked **Reaction workup** (the badge opens the investigation, which closes only when they are verified); **Result on bench** on the investigation opens the worklist on its tube. On the Specimens tab, accepting a tube says how many tests are waiting on it; **Start testing** takes you straight to them. When the patient has test orders still waiting for a tube that this one suits (its type and visit), the accept also offers **Link them**: one click links the tube to those orders (each link is audited) and their tests join the list. **Not now** leaves them for the Orders tab.

For a **numeric** test, the units fill in from the test, and the reference range shows under the value. A value outside the reportable range, or with more decimals than the test allows, is refused; a value outside the reference range shows **L** or **H**, and beyond a critical limit **LL** or **HH** with a warning to confirm and notify as your procedure requires.

For a **titer**, choose the reaction at each dilution, from the first, until the first negative one (you may read further). The endpoint (for example **Titer 1:32**) and, when the test is scored, the score appear as you go. A reaction recorded after a negative dilution is saved with a warning to check for a prozone or an entry error.

For patient ABO and Rh, the person who entered the result cannot verify it; for them the row says **Review**.

When you enter cell reactions for an ABO/Rh, a crossmatch, or a test made of subtests (such as the antibody screen), the interpretation fills in from the reactions as you go: the ABO and Rh(D) type, Compatible or Incompatible, or the panel interpretation. Check cells and phases left out of interpretation are not counted. A crossmatch is Incompatible as soon as any counted reaction is positive, and Compatible once every counted reaction is entered and negative. When the reactions are incomplete, mixed field, or match no single interpretation (an ABO discrepancy, for example), your current choice is left as it is and a note says so. Resolve a discrepancy before you save. Always check the interpretation before you save.

> Hard stop. Verify is refused when the specimen is rejected or expired (a correction of an already verified result is the exception and can be verified after expiry, OCD-051), when the order is on hold, or when you are verifying your own patient ABO or Rh entry.

## Discrepancies

If the ABO or Rh does not match history, verification stops. A supervisor chooses **Retain** (keep the historical type and do not replace it with this result) or **Replace** (accept the new type), with an electronic signature. Do not verify around the delta by editing history.

A manual ABO or Rh override, when policy allows one, is an immunohematology override. It also requires the override permission and a signature. It is not the same action as ordinary verification.

## Interface and retype rows

Results that arrived from an instrument wait for the same verification. The row marks them so you know you did not type them. Read the values.

The product retype tab of the worklist lists units that still need a front type. Recording the retype is the same action as the retype board in Inventory. A match can release the unit. A mismatch quarantines it. An outdated unit that matches is expired rather than made available.

## Correct or invalidate

After verification, a wrong result is corrected or invalidated. Correction requires a signature. You can correct a verified result, and verify the correction, after its specimen has expired: the correction fixes the record of a test already done. A new result on an expired specimen is still refused. Invalidation keeps the prior result visible and removes it from current history. Both are audited. The supervisor queue shows verifications that are waiting when your role is the one who verifies.
