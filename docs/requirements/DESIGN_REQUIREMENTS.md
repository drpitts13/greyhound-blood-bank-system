# Design requirements

| ID | Design constraint | FRS |
|---|---|---|
| SRS-BB-001 | Domain rules are pure functions. They do not read the clock or database. Application services assemble facts and call the rule. | FRS-BB-030, FRS-BB-031 |
| SRS-BB-002 | `IssueGate` is the single issue-time rule aggregation point. Additional HardStops may be appended (dual ID, second-verifier directory) but must not replace the gate. | FRS-BB-031 |
| SRS-BB-003 | Electronic XM eligibility is computed by `ElectronicCrossmatchEligibilityService` / `ElectronicCrossmatchEligibilityRule` and reused by the patient workspace and issue pathway. | FRS-BB-033 |
| SRS-BB-004 | Assign and issue persist inside one unit-of-work. Unique filtered indexes enforce one active reservation and one open issue per unit. | FRS-BB-041 |
| SRS-BB-005 | AntibodyHistory, PatientBloodTypeHistory, InventoryStatusHistory, AuditEvent, and ElectronicSignature cannot be deleted through normal application saves. | FRS-BB-002, FRS-BB-060 |
| SRS-BB-006 | SQLite development databases apply additive columns/indexes via `DevelopmentSqliteBootstrap` so unique safety indexes exist on existing files. | FRS-BB-041 |
| SRS-BB-007 | Authorization is evaluated in the Application layer, not only in the UI. | FRS-BB-040 |
| SRS-BB-008 | ISBT raw scan, parsed fields, and display values are stored separately. | URS identity / ISBT docs |
| SRS-BB-009 | A merged (losing) patient is rejected at each clinical write path (accession, order, result, immunohematology, allocate, XM, issue), not only at issue. | FRS-BB-003 |
