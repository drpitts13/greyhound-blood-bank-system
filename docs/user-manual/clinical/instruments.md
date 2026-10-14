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

When a result did not appear on the worklist, open the log for that instrument. A message that failed to map names the test code the instrument sent. Do not retype the result from the instrument printout and also leave the interface message to file later. You will have two results. Either replay after the map is fixed, or enter the result manually and resolve the interface message so it does not file again.

The simulator is a separate screen for sending a test message. Do not point it at a live instrument connection.
