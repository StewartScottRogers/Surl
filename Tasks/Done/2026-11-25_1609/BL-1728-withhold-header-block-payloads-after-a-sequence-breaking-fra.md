---
id: BL-1728
title: Withhold header-block payloads after a sequence-breaking frame or PUSH_PROMISE in Http2RequestBytesRedactor
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1714]
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1728 — Withhold header-block payloads after a sequence-breaking frame or PUSH_PROMISE in Http2RequestBytesRedactor

## Goal

After a frame that breaks a header block's sequence, or a client's `PUSH_PROMISE`, `Http2RequestBytesRedactor` logs
that frame's payload and every later header-block payload on the connection as `****`.

## Context

- Found by reading the code while planning BL-1714; not yet reproduced by a test.
- Code: `Protocols/Http/Surl.Http2.UnitLibrary/Http2RequestBytesRedactor.cs`; tests:
  `Protocols/Http/Surl.Http2.UnitTests/Http2RequestBytesRedactorTests.cs`.
- Three sequence breaks are decoded as if valid and can be logged raw: a `CONTINUATION` with no open block, a `HEADERS`
  inside an open block, and a `CONTINUATION` on another stream than its block's. RFC 9113 section 6.10 forbids all
  three. The redactor never reads the frame header's stream ID.
- `PUSH_PROMISE` (type `0x5`) carries a header block but passes through as an ordinary frame, even after the decoder is
  lost. Surl's server refuses a client's `PUSH_PROMISE` (`Http2ConnectionServer`).
- This contradicts ADR-0182 amendment 3, "Fail closed". Frame headers stay logged as received (same amendment).
- No stand-in store, event or fault: this only changes what the exchange log shows for bytes a client sent.

## Acceptance criteria

- [x] A named fast test in `Surl.Http2.UnitTests` sends a `CONTINUATION` with no open block, then a further header
      block, and finds the offending payload and every later header-block payload logged as `****`.
- [x] A named fast test does the same for a `HEADERS` frame inside an open block.
- [x] A named fast test does the same for a `CONTINUATION` on another stream than the open block's.
- [x] A named fast test does the same for a `PUSH_PROMISE` frame.
- [x] The class remarks of `Http2RequestBytesRedactor` match the behaviour.
- [x] Build with `-warnaserror` is clean, fast tests are green, and `Surl.Http2.UnitLibrary` stays at 100% line and
      branch coverage.

## Notes

Decided by Claude under Stewart's delegation; this applies ADR-0182 amendment 3, "Fail closed".

- A `HEADERS` inside an open block, a `CONTINUATION` with no open block or on another stream (the reserved stream-ID bit
  ignored, RFC 9113 section 4.1), and any `PUSH_PROMISE` lose the HPACK copy. The open block's held frames, which were
  never decoded, are written withheld too. A `PUSH_PROMISE` is never buffered.
- A frame of another type that interrupts a block still has its own payload logged as received: it carries no header
  block.
- No new state: while the copy is not lost, a held frame means a block is open, and the first one names its stream.
- Tests: `Redact_ContinuationWithNoOpenBlock_...`, `Redact_HeadersInsideAnOpenBlock_...`,
  `Redact_ContinuationOnAnotherStream_...`, `Redact_PushPromise_...`, `Redact_PushPromiseInsideAnOpenBlock_...` (all
  five failed before the change) and `Redact_ContinuationDifferingOnlyInTheReservedStreamBit_ContinuesTheBlock`.
- Gates: build `-warnaserror` clean, format clean, fast tests 29152 passed, 0 failed, 3 skipped. `Measure-CodeQuality.ps1
  -Library Surl.Http2.UnitLibrary`: 100% line, 100% branch, 0 failing members (run before two private methods were
  renamed; no logic changed after it). Integration tests not run: no socket or file code changed.
- Conformance audit skipped: no option, exit code or wire byte changes, only the exchange log.
- ADR-0182's wording is not in `touches` and BL-1713 holds that file: filed as BL-1731.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The HTTP/2 exchange log withholds header-block payloads after a sequence-breaking frame or a client's PUSH_PROMISE
