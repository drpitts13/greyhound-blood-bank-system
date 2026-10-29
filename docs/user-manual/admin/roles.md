---
slug: roles
title: Roles and permissions
order: 30
chapter: Directory and audit
summary: A role is a named set of permission codes plus a security level used for exception overrides.
routes:
  - /admin/roles
---

![Roles](figures/admin-roles.png)

**Roles** defines what a job can do. The application checks permission codes, not the role name. Renaming a role does not change access. Changing the codes on it does.

## Security level

Each role has a security level. Exception overrides in the exception catalog require the user's role to meet a minimum level as well as the permission code. A technologist role at a lower level cannot override a warning that is reserved for a supervisor level, even if someone copies a permission code onto that role by mistake. Check both the code and the level when an override "should work" and does not.

## Seeded roles

A training database includes Administrator, Supervisor, Technologist, Read only, Interface analyst, Inventory manager, Billing analyst, Auditor, and a service account. Treat those as examples. Your facility's roles should match your procedure. Do not grant `issue.emergency-release` to every technologist because the supervisor is at lunch. Emergency release is a separate permission from ordinary issue.

Open [Users](users) to see who holds a role. Changing the role changes everyone assigned to it the next time they sign in.
