---
id: BL-1717
title: Bound all that Http2RequestBytesRedactor holds for an open header block
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1714]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1717 — Bound all that Http2RequestBytesRedactor holds for an open header block

## Goal

Everything `Http2RequestBytesRedactor` holds for an open header block is bounded, whatever frames the client sends.

## Context

- Found by reading the code while planning BL-1714; not yet reproduced by a test.
- Code: `Protocols/Http/Surl.Http2.UnitLibrary/Http2RequestBytesRedactor.cs`; tests:
  `Protocols/Http/Surl.Http2.UnitTests/Http2RequestBytesRedactorTests.cs`.
- `heldBlockLength` counts payload bytes only, so endless empty `CONTINUATION` frames without `END_HEADERS` grow
  `heldFrames` without limit in the redactor. Whether a server stops such a client first is unchecked. The class remarks
  say no more than 64 KiB is ever held.
- ADR-0182 amendment 3, "Fail closed": once the redactor gives up on a block, every later header-block payload on the
  connection is withheld. How the limit counts what is held is Claude's to choose (root `CLAUDE.md`, "Decisions"); the
  class remarks state it.
- No stand-in store, event or fault: this only bounds what the exchange log's redactor keeps.

## Acceptance criteria

- [ ] A named fast test in `Surl.Http2.UnitTests` sends enough empty `CONTINUATION` frames without `END_HEADERS` to
      pass the limit, and finds the held frames written with `****` payloads before `Flush` is called.
- [ ] A named fast test finds header-block payloads sent after the limit was passed logged as `****`.
- [ ] The class remarks of `Http2RequestBytesRedactor` state the limit as implemented.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and `Surl.Http2.UnitLibrary` stays at 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-25: Created.
