# Release checklist

This is a validation-support checklist, not a certification.

- [ ] Solution builds.
- [ ] Full automated test suite passes, including `tests/safety_regression/`.
- [ ] No HardStop was downgraded to a Warning except a documented emergency conversion.
- [ ] Unique allocation/issue indexes present on the target database.
- [ ] Facility policies reviewed (`AllowElectronicCrossmatch` remains off until OCD-006 is closed).
- [ ] Open clinical decisions reviewed; none silently defaulted.
- [ ] Traceability matrix updated for the change set.
- [ ] Risk register residual ratings accepted by the quality reviewer.
- [ ] Secrets, local `.db` files, and `bin/` / `obj/` are not in the release commit.
- [ ] Downtime plan still matches deployed interface and backup procedures.
