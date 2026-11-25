---
id: BL-1714
title: Fail closed after an oversized HTTP/2 header frame in Http2RequestBytesRedactor
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1714 — Fail closed after an oversized HTTP/2 header frame in Http2RequestBytesRedactor

## Goal

After any `HEADERS` or `CONTINUATION` frame that exceeds the 64 KiB cap, `Http2RequestBytesRedactor` withholds every
later header-block payload on the connection.

## Context

- Found by the review of BL-1709, by reading the code; not yet reproduced by a test.
- In `StartFrame`, the over-limit branch calls `LoseDecoderAndWriteHeldFrames`, which returns early when no frame is
  held, so `decoderLost` stays false. A single `HEADERS` frame over the cap with no `END_HEADERS` is then followed by a
  `CONTINUATION` decoded as a block of its own against a stale table, and its bytes can be logged raw.
- ADR-0182 amendment 3 says "from then on every header-block payload on that connection is withheld".

## Acceptance criteria

- [ ] A named fast test in `Surl.Http2.UnitTests` sends an over-limit `HEADERS` frame without `END_HEADERS`, then a
      `CONTINUATION`, and finds the continuation's payload logged as `****`.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and `Surl.Http2.UnitLibrary` stays at 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-25: Created.
