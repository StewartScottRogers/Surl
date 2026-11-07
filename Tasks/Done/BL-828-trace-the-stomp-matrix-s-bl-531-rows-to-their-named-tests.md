---
id: BL-828
title: Trace the STOMP matrix's BL-531 rows to their named tests
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-531]
touches: [Documentation/Conformance/Extended/Stomp.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-828 — Trace the STOMP matrix's BL-531 rows to their named tests

## Goal

Every row of `Documentation/Conformance/Extended/Stomp.md` whose last cell is BL-531 names the test in
`Surl.Extended.Protocol.Stomp.UnitTests` that proves it instead.

## Context

- ADR-0118, Consequences: each task replaces its ID in the matrix with the named test.
- BL-531 could not edit the matrix: `Documentation/Conformance` was in another Doing task's `touches` when it finished.
- Rows whose remainder belongs to BL-532 (Frames-6, a body on the routing frames; Size-3, the bus's `--max-filesize`)
  keep BL-532 for that part; name only what BL-531's tests show.

## Acceptance criteria

- [x] Each BL-531 row in `Stomp.md` names a `Class.Method` test in `Surl.Extended.Protocol.Stomp.UnitTests` that exists
      and passes, or names BL-532 where that task owns the rest.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Stomp.md names existing test methods for all 36 BL-531 rows; Frames-6 and Size-3 keep BL-532 for the part it owns
