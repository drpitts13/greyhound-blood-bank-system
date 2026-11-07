---
slug: instruments
title: Instrument interfaces
order: 350
chapter: Interfaces and reports
summary: See whether each instrument connection is receiving messages and open the log when a result did not file.
routes:
  - /instruments
---

![Instrument interfaces](figures/instruments.png)

**Instrument Interfaces** shows the status of ASTM instrument connections and their recent messages. You need permission to manage instruments. The mapping of instrument tests onto LIS tests, and the if/then rules, are configured in Administration. The help panel on that administrator screen is the field reference. This page tells you whether messages are arriving.

The Messages list counts failed messages that are still open. Open one to see why it failed. Fix the cause (for example place the missing order) and choose **Replay**: when the replay applies, the failed message is marked resolved by that replay. If you handled it another way, such as entering the result by hand, give the reason under **Resolve without replay**. Replay is offered only for an open failed message, so results that already filed are never filed again.

When a result did not appear on the worklist, open the log for that instrument. A message that failed to map names the test or subtest code the instrument sent; a result for a test that is not ordered on the specimen says so; a result held for an instrument error code names the code.

A **held** result is one an instrument error or flag code kept off the record (the instrument's error code list says Hold). Replaying it holds it again. Check the reactions on the instrument and its QC, then either re-run the sample (and resolve this message once the new result has filed), or choose **Release held result** with the reason. The result then posts as pending verification, with the held code and who released it shown beside its source, and the message closes with your reason. Releasing needs result verification permission. **unmapped** beside Messages counts instrument codes with no translation and opens the list. A result for a specimen that has expired, was rejected, or is otherwise not accepted is refused with the same message you would see entering it by hand, including when the instrument re-sends a result it sent earlier. The earlier result is left as it was. Do not retype the result from the instrument printout and also leave the interface message to file later. You will have two results. Either replay after the map is fixed, or enter the result manually and resolve the interface message so it does not file again.

The simulator is a separate screen for sending a test message. Do not point it at a live instrument connection.
