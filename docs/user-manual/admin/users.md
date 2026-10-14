---
slug: users
title: Users
order: 20
chapter: Directory and audit
summary: Create accounts, assign roles, and lock or deactivate a user. Privilege increases require a reason.
routes:
  - /admin/users
---

![Users](figures/admin-users.png)

**Users** lists accounts. **New user** creates one. **Manage** on a row edits it. Roles themselves are defined on [Roles](roles). This screen assigns those roles to a person.

## What to set

The username is what the person types at sign-in. The display name is what other screens show. A service account is for an interface, not for a person at a bench. Do not give a shared bench password to a service account, and do not let people sign in as the service account to bypass their own role.

Status is active, locked, or inactive. Lock is what you do after five failed passwords or when someone should be stopped immediately. Inactive is for someone who has left. Both end the ability to sign in. An open session is revoked when the account is locked or deactivated.

## Privilege changes

Adding a role that grants more permission, especially emergency release, result correction, user administration, or configuration edit, requires a reason. The reason is audited. Removing a role should also say why. After you change roles, tell the person to sign out and sign in. The session they already have keeps the old permissions until it ends.

> Warning. An administrator account can change every clinical rule. Do not use it for routine bench work. The audit trail will attribute those results and issues to the administrator.
