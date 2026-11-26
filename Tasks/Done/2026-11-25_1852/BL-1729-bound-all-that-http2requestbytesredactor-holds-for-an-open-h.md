---
id: BL-1729
title: Bound all that Http2RequestBytesRedactor holds for an open header block
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1714]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1729 — Bound all that Http2RequestBytesRedactor holds for an open header block

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

- [x] A named fast test in `Surl.Http2.UnitTests` sends enough empty `CONTINUATION` frames without `END_HEADERS` to
      pass the limit, and finds the held frames written with `****` payloads before `Flush` is called.
- [x] A named fast test finds header-block payloads sent after the limit was passed logged as `****`.
- [x] The class remarks of `Http2RequestBytesRedactor` state the limit as implemented.
- [x] Build with `-warnaserror` is clean, fast tests are green, and `Surl.Http2.UnitLibrary` stays at 100% line and
      branch coverage.

## Notes

- Limit (Claude's choice, as Context allows): each held frame counts as its 9-byte frame header plus its payload against
  the one 64 KiB cap, so at most 7,281 frames are held. A frame count of its own was not needed. No ADR: ADR-0182
  decision 6 and amendment 3 ("one past 64 KiB") stay true, and the class remarks state the counting rule.
- Upstream curl, measured while planning (pinned static curl 8.21.0, Windows, `--http2-prior-knowledge`): a 60,000
  character `-H` value is sent as four frames counting 60,077, under the cap; at 70,000 curl sends no header block and
  exits 55.
- Tests: `Redact_EmptyContinuationsPastTheCap_WritesTheHeldFramesWithheldBeforeFlush` (box 1),
  `Redact_HeaderBlockFramesAfterEmptyContinuationsPassTheCap_WithholdsTheirPayloads` (box 2), and three boundary tests.
- Verified: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; fast tests 29,172 passed,
  0 failed, 3 skipped; `Measure-CodeQuality.ps1 -Library Surl.Http2.UnitLibrary` 0 failing members.
- Conformance stage skipped: no option, exit code or byte sent or accepted changes.
- `align-and-document` also brought the library's `CLAUDE.md` up to date (who references it; the redactor listed).
- Filed BL-1737 (a false sentence in the Abstractions `CLAUDE.md`). ADR-0182 wording is already BL-1730 and BL-1731.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. Http2RequestBytesRedactor counts frame headers and payloads against its 64 KiB cap, so empty CONTINUATIONs cannot grow what it holds
