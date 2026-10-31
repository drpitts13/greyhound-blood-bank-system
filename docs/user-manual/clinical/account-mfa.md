---
slug: account-mfa
title: Authenticator app
order: 25
chapter: Getting started
summary: Set up an authenticator app so sign-in needs a 6-digit code as well as your password.
routes:
  - /account/mfa
---

**Authenticator** (under your name in the sidebar) sets up a second sign-in step with an authenticator app on a phone or other device, such as Microsoft Authenticator or Google Authenticator.

## Set it up

1. Select **Set up an authenticator**. The page shows a key, plus a setup link for apps that accept one.
2. In the app, add an account and type the key.
3. Enter the 6-digit code the app shows and select **Confirm**.

The key is shown only once. It is stored protected and cannot be displayed again. If you leave the page before confirming, start again.

## Signing in

After your password is accepted, the sign-in page asks for the current code. A code works for about 30 seconds, plus one step either side to allow for clock differences, and each code can be used only once. A wrong code counts as a failed sign-in, and five in a row lock the account.

## Facility policy

Authenticator sign-in is a facility option: **Enable authenticator-app sign-in** in Facility Policies (default off). While it is off, nobody is asked for a code and setup is unavailable.

With it on, **Require MFA for privileged users** or **Require MFA for all users** decides who must have an authenticator. Someone in scope who has none **cannot sign in**: after the password, the sign-in screen shows a key to add to the app, and the first valid code completes setup and signs in. They cannot remove their authenticator themselves. Anyone else can remove theirs with a current code.

## Lost device

Ask an administrator to select **Reset authenticator** on your user in Admin › Users. Then set it up again. Administrators cannot reset their own.
