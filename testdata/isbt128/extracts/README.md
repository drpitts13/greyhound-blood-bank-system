# ICCBBA extract drop folder

Place facility-exported ICCBBA tables here when a license is available.

Accepted: `.csv`, `.tsv`, `.txt`, `.json`

Rejected: Microsoft Access (`.mdb`, `.accdb`) and Excel (`.xlsx`, `.xls`).
Export product and ABO/RhD tables first (ST-010 is licensed; this software
does not parse those databases).

File-name hints:

- `*.json` — `{ "productCodes": [...], "aboRhdCodes": [...] }`
- name contains `abo`, `rhd`, or `bloodgroup` — ABO/RhD delimited extract
- otherwise — product description codes

Do not commit licensed extracts. An empty folder keeps US-public-subset
placeholders. Import is an administrator action (`admin.config.edit` plus
license acknowledgment). Startup does not auto-import.
