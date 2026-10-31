---
slug: database-backups
title: Database backups
order: 45
chapter: Directory and audit
summary: Verified database backups on a schedule and on demand, with the audit manifest a restore is checked against.
routes:
  - /admin/backups
---

**Database Backups** shows the last verified backup and the recent attempts. Administrators with configuration edit permission can select **Back up now**.

## What a backup does

- **SQLite** (development and small installs): the system writes a consistent copy with `VACUUM INTO`, opens the copy read-only, and runs an integrity check. The copy's size and SHA-256 hash are recorded.
- **SQL Server**: the system runs a copy-only `BACKUP DATABASE … WITH CHECKSUM`, then `RESTORE VERIFYONLY … WITH CHECKSUM`. The backup file is written on the database server, so the folder must be a path the SQL Server service can write. A copy-only backup does not disturb the facility's own backup chain.

A backup counts only when it was written and verified. Each attempt, good or failed, is logged here and in the audit trail.

## Manifest

Each backup records the number of audit events and the hash of the last audit event inside it. After a restore, compare the audit trail with that manifest (Downtime plan, section 4). A matching count and last hash show that no audit rows were lost up to the backup.

## Schedule, retention, and alerts

Settings are in the `Backup` section of the API configuration:

- `Folder`: where backups are written. Default `backups` under the API folder.
- `IntervalHours`: how often a scheduled backup runs. Default 24. Set 0 to turn scheduled backups off.
- `KeepCount`: how many verified backup files to keep. Default 14. Older files are deleted from the folder; their log rows stay.
- `OverdueHours`: when to raise the overdue alert. Default 48.

A failed backup raises a critical alert. While there is no verified backup inside the overdue window, a critical "No verified database backup" alert stays open; it closes on its own after the next verified backup.

Backups in the same folder as the database do not protect against losing that disk. Copy the backup folder to separate storage as your facility procedure requires.
