---
slug: signing-in
title: Signing in
order: 20
chapter: Getting started
summary: Sign in with your username, password, and workstation. Sessions expire, and five failed attempts lock the sign-in.
routes:
  - /login
---

![Sign in](figures/signing-in.png)

Open the application. If you are not signed in, the sign-in screen asks for three things.

1. **Username.** This is the account your administrator created. It is not your display name.
2. **Password.**
3. **Workstation.** This is recorded on the audit trail with your actions. The field starts as the computer name. Change it when you are working at a different bench or when your procedure names the workstation.

Choose **Sign in**. A wrong password or an unknown account shows an error and does not open a session. After five failed attempts the account is locked until an administrator unlocks it.

## After you sign in

The sidebar shows only the areas your permissions allow. The dashboard lists those permission codes under **Your access**. A role change does not apply to a session that is already open. Sign out and sign in again after an administrator changes your roles.

## How long a session lasts

The session ends after 30 minutes without activity, and it ends 12 hours after sign-in even if you are still working. Signing out, an administrator lock, or deactivating the account also ends it. When the session ends, the next action returns you to the sign-in screen. Finish or save the work you are in before you leave the bench.

> Warning. Do not share a signed-in session. The audit trail records the signed-in user as the person who performed the action.

## Training database

A training or development database may show demo accounts on the sign-in screen. Those accounts are described in [Training data](training-data). Do not use a demo password on a clinical system.
