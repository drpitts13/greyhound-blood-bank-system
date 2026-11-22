---
slug: report-catalog
title: Report catalog
order: 420
chapter: Reports
summary: Open the list of report definitions. Running a report stays on the Reports screen in the clinical guide.
routes:
  - /admin/reports
---

![Report catalog](figures/admin-reports.png)

**Report designer** in Administration opens the catalog of definitions. Running and export stay on Reports in the clinical application, and they require run permission rather than design permission. A user who can design can change what every runner sees. Limit design permission to people who should change published reports.

Open a report to edit it, or create one. Unfinished drafts should stay unpublished so the run list does not offer a query you have not checked. A published report with a broken query fails at run time for the technologist. Run it yourself after you publish.

Deleting or replacing a published report does not delete files people already exported. It changes the next run.

System reports (those that came with the system) cannot be edited; clone one to change it. Their columns follow the catalog, so when a system report gains a column it appears in existing installations at the next start. For example, **Emergency releases and overrides** now has **Physician statement** for each emergency or massive transfusion issue: *Not needed* when testing was complete at issue, *Not received*, or *Received from* the physician who signed it (recorded on the Retrospective board). The SQL view `rpt_overrides` has the same column, `physician_statement`.

**RhIG evaluations and doses given** lists each RhIG evaluation by event (antepartum, postpartum or other exposure) with the decision, vials recommended, when it was completed and, when RhIG was given, the time, vials, lot, who gave it and the whole hours from the event, for the review of RhIG practice against your procedure. The SQL view is `rpt_rhig`.

**Patient antibody record** lists every antibody on file for a patient: MRN, name and date of birth, the antibody, its status, whether it is active (and why it was deactivated), and whether it was identified in this laboratory or reported by another facility, with when that report was confirmed here. Use it for the periodic review of antibody records, before a planned outage, or to answer a request from another facility. The SQL designer reads it as `rpt_antibodies`.

**Patient special requirements** lists every special transfusion requirement on file (irradiated, CMV-negative, antigen-negative, call the physician and the others): the patient, the requirement as named in the catalog, the antigen, the reason, when it took effect and ends, whether it is active, and who entered it. Use it to review requirements that should end or to answer another facility. The SQL designer reads it as `rpt_special_requirements`.

**Reagent QC record** lists every reagent QC reading for the period you choose: when it was performed, the reagent, lot, control, expected and observed reactions, pass or fail, who performed it and who reviewed it (blank until reviewed). Use it for the QC record of a month or an inspection; the Reagents page shows only the last 14 days. The SQL view is `rpt_reagent_qc`.

**Equipment check record** lists every equipment check recorded, across all equipment, for the period you choose: when, the equipment (code, name, kind, location, serial number), the check, pass or fail, who performed it and the note. Use it for the month's maintenance record; each piece of equipment's History still lists its own checks. The SQL view is `rpt_equipment_checks`.
