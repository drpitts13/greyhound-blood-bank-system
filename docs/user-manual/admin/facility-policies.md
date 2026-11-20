---
slug: facility-policies
title: Facility policy
order: 250
chapter: Rules and safety
summary: Set specimen windows, dual verification, electronic crossmatch, inventory receipt, and retention. Each edit requires a reason.
routes:
  - /admin/facility-policies
---

![Facility policy](figures/admin-facility-policies.png)

**Facility Policy** holds the parameters that drive clinical behavior: how long a specimen is valid, when a second person must verify, whether electronic crossmatch is allowed, how inventory receipt works, and how long records are retained. The page groups them by category. Filter by name, key, or citation.

Each row shows the current value and the citation the parameter was written against. When the system starts, any parameter the database does not have yet is stored at the default shown here, and Configuration History records it as added by the system. A value your facility has already saved is never changed. **Require second verifier at issue** defaults to on: a facility that does not require one turns it off here with a reason. Select the row to edit. Boolean parameters are true or false. Numeric parameters show the allowed range. A value outside the range is rejected. Every save requires a reason.

> Facility policy. Electronic crossmatch stays off until you set the policy that allows it. Turning it on does not by itself make any patient eligible. Antibody history, open workups, and the minimums in Crossmatch Settings still apply.

**Settings to enter.** Some settings have been decided as policy, but their value is your facility's to enter:
- the three *RhIG dose* settings;
- *RhIG: give within (hours of the event)*;
- *Lookback: notify the physician within (days)*: the days after a lookback notification is opened within which the recipient's physician is to be notified. One value applies to every lookback type (a recipient, a facility the unit was shipped to, a recall); the lookback list and a DIN search show each notification's notify-by date from it. It ships 84 days (OCD-089, decided 2026-11-14); 0 means not set;
- *Storage temperature reading interval (hours)*: how long a refrigerator, freezer or platelet incubator with a temperature range may go without a reading before Storage temps marks it reading due and the home card counts it (end-user testing pass 87); 0, the default, means not set and it is listed to enter;
- *STAT test turnaround target (minutes)*: how long a STAT, urgent, emergency-release or massive transfusion test may wait from order to verification before the Test Worklist marks it past the target, lists it first among rush work and the Pending tests card counts it (end-user testing pass 106); 0, the default, means not set and nothing is marked;
- *Specimen retention days*.

While such a setting is still at its "not set" value, its row is marked **Not set**. A note at the top of the page names all of them, with **Show only these** to list just them. The home card **Facility settings to enter**, for those who can edit configuration, counts them and opens this page filtered to them. Until a value is entered, what depends on it does not happen; for example, no RhIG dose is calculated. Settings that are off by choice, such as the neonatal rules, are not counted.

The open-system and pooled thawed shelf lives are entered per product code on **Auto thaw products**, not here. An active code without one of them is named in a second note on this page (for example *E0707 (open system, pooled)*), with a link to Auto thaw products filtered to those codes, and counts on the same home card.

A parameter marked legal hold should not be edited to satisfy a convenience. The badge is there so you see the hold before you save.

Links on this page open [Crossmatch settings](crossmatch-settings) and [Compatibility tables](compatibility-rules). Those are separate catalogs. A change here does not edit the ABO table.

The bench feels a policy change on the next action. It does not reprint old issues. Say in the reason which procedure version you are implementing.

Three operational settings ship off or at a default that only warns. **Block results when reagent QC is open** stays off, so patient resulting continues while the reagent worklist has overdue, failed, or unreviewed rows. **Final disposition due hours** ships at 24 and only fills the warning board. **Require supply lot** stays off, so a modification can be saved without a lot. **Require irradiator and dose indicator** stays off too (OCD-067): an irradiation can be saved without them, but an indicator reported as not showing irradiated is always refused.

**Require consent confirmation at the bedside** (Transfusion) ships off. When on, starting or documenting a transfusion on Issue & Transfuse needs the box *The patient's consent to transfusion is on the chart* ticked; the confirmation is recorded with the transfusion whenever it is ticked, on or off. Transfusions reported by the EHR are not affected. Turn it on only if your procedure has the bedside check consent.

## Medical director decisions

Decisions that belong to the transfusion service medical director are facility settings, so a facility whose medical director decides differently changes the setting instead of the software. Each ships at the decision recorded for this application (in the Open clinical decisions record), so an existing facility sees no change until it edits one. Each edit needs a reason and is kept in Configuration History.

| Setting | Category | Ships at | Choices |
| --- | --- | --- | --- |
| *FDA reportability needed to close a deviation* | Quality | 2 | 0 none; 1 every deviation; 2 major and critical; 3 critical only (OCD-090) |
| *C:T ratio target (tenths)* | Quality | 0 | The C:T ratio Blood utilization marks lines above, in tenths (20 = 2.0); 0 not set (pass 342) |
| *Open a deviation for an incompatible retrospective crossmatch* | Issue | on | off raises the critical alert and warning without a deviation (OCD-046) |
| *Retrospective crossmatch on the expired pre-transfusion specimen* | Compatibility | on | off needs a specimen in date (OCD-050) |
| *Emergency release before a specimen is drawn* | Issue | on | off refuses emergency and MTP release until a specimen is received (OCD-053) |
| *Reaction workup on an expired pre-transfusion specimen* | Reaction | on | off refuses an expired specimen for the workup (OCD-058) |
| *Closing a reaction needs the conclusion and treatment* | Reaction | on | off records them when given without requiring them (OCD-057) |
| *RhIG: weak D and partial D* | RhIG | 1 | 1 both treated as Rh negative; 2 partial D only, weak D follows the type on file (OCD-069) |
| *Another unit while a reaction is under investigation* | Issue | 1 | 0 no check; 1 warning with an override; 2 refused until the investigation closes (OCD-078) |
| *Back-entry outside a recorded outage* | Downtime | 1 | 0 allowed; 1 needs a reason; 2 refused (OCD-081) |
| *Verify a result after its specimen expires* | Result | on | off: verify before the specimen expires (OCD-093) |
| *Verify a correction after its specimen expires* | Result | on | off: the correction needs the specimen in date (OCD-051) |
| *Rationale to identify an excluded specificity* | Antibody ID | on | off leaves the warning only (OCD-028) |
| *Complete identification on an expired specimen* | Antibody ID | on | off refuses completion and acceptance instead of asking for an acknowledgment (OCD-025) |
| *Complete identification after the panel lot expired* | Antibody ID | on | off refuses completion and acceptance (OCD-027) |
| *Antigens that show dosage* | Antibody ID | C, c, E, e, FYA, FYB, JKA, JKB, M, N, S, s | antigen codes, case-sensitive (OCD-020) |
| *Antigen typing methods read as predicted genotype* | Antibody ID | genotype, molecular, predicted, dna, pcr | words in the method (OCD-024) |
| *Inbound result statuses that post* | Interface | F, C, P, R | OBX-11 letters; a blank status always posts (OCD-019) |
| *Tests verified in a batch* | Result | ABORH, ABSC, DAT | any of those three (OCD-061) |
| *Titer score per grade* | Result | 0=0, w+=2, 1+=5, 2+=8, 3+=10, 4+=12 | grade=score, 0 to 20 (OCD-065) |
| *Receipt temperature: lowest / highest (°C)* | Inventory | 1 and 10 | whole degrees (OCD-005) |
| *Refuse clinical use of an inactive patient* | Patient | off | on refuses new orders, specimens, results, crossmatch and allocation, and issue for an Inactive record (OCD-009) |
| *Identification on a received, not yet accepted, specimen* | Antibody ID | on | off refuses opening, completing and accepting until the specimen is accepted (OCD-026) |
| *Transfusion reported for a unit no longer issued* | Transfusion | 1 | 1 record, keep the unit's status, flag for review; 2 hold the message unrecorded among HL7 errors (OCD-083) |
| *Paper patient test on a specimen expired since* | Result | 2 | 0 refused; 1 tested before expiry with a reason; 2 also in a recorded outage (OCD-091) |
| *Paper crossmatch on a specimen expired since* | Result | 1 | same choices; also governs whether such a specimen counts at a downtime back-entry issue (0 does not) (OCD-091) |

A list setting is typed comma-separated; a value that does not fit its list (for example a status that is not one capital letter, or a test other than ABORH, ABSC or DAT) is refused when saved and says why.

Earlier medical director decisions were already settings here: specimen validity windows, uncrossmatched group O and O negative, antibody identification exclusion counts and supervisor review, the neonatal rules, RhIG dose and window, lookback notify-by, specimen retention, and the reminders. Rules that keep an ABO-incompatible or wrong-patient unit from being given, and labelling requirements, are not settings.
