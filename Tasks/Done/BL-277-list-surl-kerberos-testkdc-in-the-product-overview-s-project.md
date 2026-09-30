---
id: BL-277
title: List Surl.Kerberos.TestKdc in the Product Overview's project map
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-266]
touches: [Documentation/Product/Product-Overview.md]
requirement: FR-046
created: 2026-09-30
completed: 2026-09-30
---
# BL-277 — List Surl.Kerberos.TestKdc in the Product Overview's project map

## Goal

`Documentation/Product/Product-Overview.md`'s project map names `Surl.Kerberos.TestKdc.UnitLibrary`
and `Surl.Kerberos.TestKdc.UnitTests` as the test-fixture pair ADR-0065 decision 1 added.

## Context

- BL-266 created the pair (the hand-built loopback KDC for realm `SURL.TEST`); it could not edit
  the Product Overview because BL-214, in Doing at the time, touched that file.
- The places to change: the layers table (around the "Hand-built primitives" row, where
  `Surl.Kerberos` is named), the "added later" sentence that counts the libraries added after
  ADR-0002, and the library/test pair table (after `Surl.Kerberos.UnitLibrary`, sorted as in
  `Surl.slnx`, where the TestKdc pair sorts just before it).
- It is a test fixture, not a protocol server or a horizontal library protocol servers
  reference: say so, and that only test projects (and BL-267's `Run-KerberosTestKdc.cs`)
  reference it. ADR-0002 decision 3's table is the list of what protocol servers reference, so
  it does not change.
- `Surl.Kerberos.TestKdc.UnitLibrary/CLAUDE.md` states what the library does.

## Acceptance criteria

- [x] `Documentation/Product/Product-Overview.md` names `Surl.Kerberos.TestKdc.UnitLibrary` and
      `Surl.Kerberos.TestKdc.UnitTests` in its project map, as ADR-0065's loopback test KDC
      that only test projects reference.
- [x] Every other statement in the Product Overview about the set of projects is still true.

## Notes

- Filed by BL-266.
- Added a "Test fixtures" row to the layers table (depends on `Surl.Kerberos` and Abstractions, per the csproj), counted the pair in the "added later" sentence, and listed it before `Surl.Kerberos` in the pair table as `Surl.slnx` sorts it. `Run-KerberosTestKdc.cs` does not exist yet (BL-267 is in Doing), so the overview names it as intent.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The Product Overview's project map lists Surl.Kerberos.TestKdc.UnitLibrary and .UnitTests as ADR-0065's loopback test KDC
