---
slug: exceptions
title: Exceptions
order: 280
chapter: Rules and safety
summary: Set the minimum security level required to override each clinical warning.
routes:
  - /admin/exceptions
---

![Exceptions](figures/admin-exceptions.png)

**Exceptions** lists clinical warnings that can be overridden and the minimum security level required. A user needs both the relevant permission (such as issue override or emergency release) and a role whose security level meets this minimum.

Raising the level stops a role that used to override the warning. Lowering it lets a lower level override. Do that only when the medical director accepts the risk. The reason you type is the change-control note.

A hard stop is not in this catalog as something you can soften. If a check has no override, it is not an exception. Do not look for a row that turns a hard stop into a warning. Emergency release is a different issue type, described in the clinical guide, not a switch on this page.
