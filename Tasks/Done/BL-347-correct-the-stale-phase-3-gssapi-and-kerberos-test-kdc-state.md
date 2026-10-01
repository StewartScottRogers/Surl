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
completed: 2026-10-01
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

- [x] No sentence in `Documentation/Planning/Roadmap.md` or `Documentation/Product/Product-Overview.md`
      names BL-242, BL-260, BL-267, BL-268 or BL-269 as pending when it is Done.
- [x] Every test, type and ADR the corrected sentences cite exists.

## Notes

- Done in the session (a two-file docs correction, no `.cs` change, so `verify` was not needed).
- Roadmap Milestone 3: the status now says SASL `GSSAPI` is proven with the Windows reference build
  through ADR-0065's test KDC (`UpstreamCurlLogsInToSurlWithKerberosTests`) and a refused ticket's
  reason reaches the verbose log (BL-260). Left open: `EXTERNAL` over mail (still unit tests only,
  since no mail Conformance test uses it) and the Linux and macOS results. ADR-0065 added to Decisions.
- Overview "What pinned upstream curl has proven": the same correction, naming the mail cases'
  shape (HTTP Negotiate is in that class too, but this paragraph is about the mail servers).
- Overview "Layers" row: `Surl.Kerberos.TestKdc` is referenced by its tests, `Surl.Conformance.UnitTests`
  (csproj checked) and `Run-KerberosTestKdc.cs` (`#:project` checked).
- Checked to exist: `UpstreamCurlLogsInToSurlWithKerberosTests`, ADR-0057, ADR-0065, `Run-KerberosTestKdc.cs`,
  the `Kerberos: ticket expired` note (`GssapiSaslMechanismTests`, `LevelledExchangeLogFactoryTests`).
- No BL-242/260/267/268/269 reference remains in either file.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Roadmap Milestone 3 and the product overview now say SASL GSSAPI is proven through the ADR-0065 test KDC and who references Surl.Kerberos.TestKdc
