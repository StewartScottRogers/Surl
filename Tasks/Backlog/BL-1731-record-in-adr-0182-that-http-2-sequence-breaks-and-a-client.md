---
id: BL-1731
title: Record in ADR-0182 that HTTP/2 sequence breaks and a client's PUSH_PROMISE are withheld
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1728]
touches: [Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1731 — Record in ADR-0182 that HTTP/2 sequence breaks and a client's PUSH_PROMISE are withheld

## Goal

ADR-0182 describes what `Http2RequestBytesRedactor` withholds after a broken header-block sequence or a client's
`PUSH_PROMISE`.

## Context

- File: `Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md`.
- Amendment 3 says "Every other frame streams through as received", and its "Fail closed" bullet lists only an
  undecodable block, one past 64 KiB and one interrupted by another frame. Amendment 7 point 2 repeats "every other
  frame as received".
- BL-1728 added: a `HEADERS` inside an open block, a `CONTINUATION` with no open block, a `CONTINUATION` on another
  stream than its block's (RFC 9113 section 6.10), and a client's `PUSH_PROMISE` (section 8.4). The behaviour is in
  `Protocols/Http/Surl.Http2.UnitLibrary/Http2RequestBytesRedactor.cs` (class remarks, `FitsHeaderBlockSequence`,
  `LoseDecoderAndStartWithheldFrame`).
- BL-1713 and BL-1730 also edit this file. Give the new amendment the next free number at the time of writing, and
  check whatever they added for the same claim.
- Documentation only: no store, event or fault for the stand-in changes.

## Acceptance criteria

- [ ] A new amendment, marked "decided by Claude under Stewart's delegation", lists the four cases and says the held
      frames, the offending payload and every later header-block payload are logged as `****`, with frame headers as
      received.
- [ ] No statement in ADR-0182 still says that a client's `PUSH_PROMISE` payload is logged as received.

## Notes

## Log

- 2026-11-25: Created.
