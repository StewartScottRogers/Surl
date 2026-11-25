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
completed:
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

- [ ] A named fast test in `Surl.Http2.UnitTests` sends a `CONTINUATION` with no open block, then a further header
      block, and finds the offending payload and every later header-block payload logged as `****`.
- [ ] A named fast test does the same for a `HEADERS` frame inside an open block.
- [ ] A named fast test does the same for a `CONTINUATION` on another stream than the open block's.
- [ ] A named fast test does the same for a `PUSH_PROMISE` frame.
- [ ] The class remarks of `Http2RequestBytesRedactor` match the behaviour.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and `Surl.Http2.UnitLibrary` stays at 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
