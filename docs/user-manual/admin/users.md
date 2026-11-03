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

**Reset authenticator** removes a user's authenticator app, for example after a lost phone. The user sets it up again; if facility policy requires it for their role, they are sent to the setup page at their next sign-in. You cannot reset your own; another administrator must. The reset is recorded in the audit trail.

## Privilege changes

Any change to a person's roles, adding or removing, requires a reason of at least 8 characters, and so does activating, deactivating, or unlocking an account. Locking does not, because it stops access. The reason and the account as it was before are kept in the change history. When roles change, the person's open sessions end at once, so they sign in again with the new permissions.

> Warning. An administrator account can change every clinical rule. Do not use it for routine bench work. The audit trail will attribute those results and issues to the administrator.
