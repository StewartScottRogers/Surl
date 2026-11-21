---
id: BL-1536
title: Name the out-of-space rule in the HTTP PUT refusal note (ADR-0129 amendment 1)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1534]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1536 — Name the out-of-space rule in the HTTP PUT refusal note (ADR-0129 amendment 1)

## Goal

The HTTP PUT exchange-log note for an upload refused by an out-of-space rule names the rule instead of an upload limit.

## Context

ADR-0129 amendment 1 (BL-1524) decides this; read it first. The wire bytes do not change.

## Acceptance criteria

- [ ] Note, when RefusedByFaultRuleId is set, is exactly: the upload was refused by the fault rule <id> (out-of-space).
- [ ] With it null the note is unchanged, and the 413 response bytes are unchanged (tests pin both).
- [ ] The library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
