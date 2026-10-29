---
slug: instrument-simulator
title: Instrument simulator
order: 360
chapter: Interfaces and reports
summary: Send a test ASTM message to confirm a mapping. This is a testing tool, not a clinical result entry screen.
routes:
  - /instruments/simulator
---

![Instrument simulator](figures/instrument-simulator.png)

The **Instrument Simulator** sends an ASTM message you compose into the interface so you can see how it maps. Use it when an administrator has changed a test map and you need a dry run, or when you are investigating a message format.

A message the simulator files is a real result if it matches a real specimen and the interface accepts it. Do not send a message that names a clinical accession unless you intend to create that result. Use a training accession on a training database.

This screen does not replace result entry. Clinical results are entered or verified on the worklist, including results that arrived from a real instrument.
