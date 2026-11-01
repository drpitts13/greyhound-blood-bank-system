---
slug: hl7-setup
title: HL7 interface setup
order: 380
chapter: Interfaces and printers
summary: Configure each HL7 interface: direction, address, vendor preset, field mapping, and where translations are edited.
routes:
  - /admin/hl7
---

![HL7 interface setup](figures/admin-hl7.png)

**Interface Setup** defines each HL7 connection: the type of message, the direction, the address and port, a vendor preset when you start from one, and the field mapping. The operational log, where you replay a failed message, is the HL7 console in the clinical guide. This page changes what the connection does.

Edit a live interface only when you can watch the next message. A bad map files orders or results on the wrong field, or rejects every message. The translation table for coded values is [Interface translations](hl7-translations). A map that expects a translated code will fail until the translation exists.

> Warning. Activating an interface that points at a production host sends and accepts real messages. Confirm the host and the port before you activate. A training system must not point at the clinical interface engine.

Vendor presets are a starting map, not a validation. Compare the preset with the specification you received from the sending system before you go live.

## Read-only FHIR API

Besides HL7 v2, the system offers a read-only FHIR R4 API at `/fhir` for the EHR. It cannot create, change, or delete anything.

- It serves the patient, the current ABO/Rh and active antibodies, special transfusion requirements, orders, and blood product status. A superseded blood type or a retired antibody is never sent.
- The caller must be signed in. Every read that returns a patient's data is recorded in the audit trail as a patient access, marked FHIR.
- Codes use this system's own code systems with readable text. Mapping to LOINC or SNOMED CT, and how the EHR authenticates, must be agreed with your interface team before go-live.
- `/fhir/metadata` lists what the API supports.

