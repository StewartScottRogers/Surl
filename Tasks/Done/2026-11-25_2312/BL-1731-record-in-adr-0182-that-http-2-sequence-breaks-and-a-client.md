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
completed: 2026-11-25
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

- [x] A new amendment, marked "decided by Claude under Stewart's delegation", lists the four cases and says the held
      frames, the offending payload and every later header-block payload are logged as `****`, with frame headers as
      received.
- [x] No statement in ADR-0182 still says that a client's `PUSH_PROMISE` payload is logged as received.

## Notes

- Added as amendment 15 (the highest before it was 14). It also states that later `DATA` payloads are withheld, which
  the code does once the decoder copy is lost (amendment 11, point 2).
- The two "every other frame ... as received" statements (amendment 3, amendment 7 point 2) now except amendments 11
  and 15; amendment 3's "Fail closed" bullet points to amendment 15. The words amendment 11 point 5 quotes are kept.
- Checked against `Http2RequestBytesRedactor.cs` (`StartFrame`, `FitsHeaderBlockSequence`,
  `LoseDecoderAndStartWithheldFrame`, `MustWithholdData`).
- Edited in the session rather than through `align-and-document`: one file, with the facts already read. No `.cs` or
  project file changed, so the `verify` skill was not needed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. ADR-0182 amendment 15 records what the HTTP/2 redactor withholds after a broken header-block sequence or a client PUSH_PROMISE
