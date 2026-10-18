---
slug: subtests
title: Subtests
order: 60
chapter: Catalog
summary: Define reusable panel rows: graded reactions, pick lists, or free text, with a polarity the logic tables can read.
routes:
  - /admin/subtests
---

![Subtests](figures/admin-subtests.png)

**Subtests** are the rows inside a panel: each cell or each phase reading that a test definition can include. A subtest has a code, a result style (graded reaction, pick list, or free text), and a polarity so interpretation tables know which answers count as positive. The list keeps the subtest code. When the name is blank, the list says Subtest and the id. That wording is not saved, and the edit form still shows the stored name.

Create a subtest once and attach it to the tests that need it. Editing the meaning of a graded reaction changes every test that uses that subtest. If only one test should change, create a new subtest rather than redefining a shared one.

Phases assigned to panel subtests are maintained on [Phases](phases). The subtest says what is being read. The phase says when it was read.
