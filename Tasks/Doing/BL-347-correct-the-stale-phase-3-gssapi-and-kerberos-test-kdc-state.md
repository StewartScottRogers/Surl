---
id: BL-347
title: Correct the stale Phase 3 GSSAPI and Kerberos test-KDC statements in the roadmap and product overview
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Roadmap.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-10-01
completed:
---
# BL-347 — Correct the stale Phase 3 GSSAPI and Kerberos test-KDC statements in the roadmap and product overview

## Goal

The roadmap's Milestone 3 and the product overview's Phase 3 sections say truly what BL-242,
BL-260, BL-267, BL-268 and BL-269 have done.

## Context

- Found by BL-319: Roadmap Milestone 3 and the overview's "Built for Phase 3" proof still say SASL
  `GSSAPI` waits on BL-242 and BL-260, both now Done. The overview's "Layers" row still says BL-267's
  `Run-KerberosTestKdc.cs` is "to be" the other user.
- Check each statement against the code and the Done tasks (including archives under `Tasks/Done`).

## Acceptance criteria

- [ ] No sentence in `Documentation/Planning/Roadmap.md` or `Documentation/Product/Product-Overview.md`
      names BL-242, BL-260, BL-267, BL-268 or BL-269 as pending when it is Done.
- [ ] Every test, type and ADR the corrected sentences cite exists.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
