---
id: BL-049
title: Escape peer bytes and peer-derived note text in the verbose log in Surl.Output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-016]
touches: [Surl.Output.UnitLibrary, Surl.Output.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-049 — Escape peer bytes and peer-derived note text in the verbose log in Surl.Output

## Goal

The verbose exchange log (BL-016) renders every byte received, every byte sent and every
note's text by ADR-0006's escaping rule, so no byte a peer chose reaches the operator's
terminal as a control sequence, and the rendering can be reversed exactly.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`,
  section 3 ("What a peer may learn"), third point. The rule, byte by byte:
  - 0x20 to 0x7E except backslash (0x5C): the byte as itself;
  - CR (0x0D) as `\r`, LF (0x0A) as `\n`; the log may start a new line after `\n`;
  - every other byte - the other C0 controls including ESC (0x1B), DEL (0x7F), every
    byte from 0x80 up, and backslash itself - as `\xHH` with two upper-case hex digits.
  Bytes Surl sends are rendered the same way.
- Notes: `IExchangeLog.Note(string)` carries no marker of which part of its text came
  from a peer (a path, a header value, a user name). The simplest rule that covers it is
  to render every note's text as its UTF-8 bytes through the same function; state that
  in the writer's XML doc. The verbose log is the operator's and may still name local
  paths and exception messages (section 3, last point) - escaping changes how they are
  shown, not whether.
- BL-016 writes the log in BL-003's line format and may already render some bytes;
  align that rendering with the rule above rather than adding a second one. One
  rendering function, used for both directions and for notes.
- `Surl.Output.UnitLibrary/CLAUDE.md`: write only to injected `TextWriter`s.
- This task changes no byte on the wire, so there is nothing to measure against upstream
  curl; the bytes it renders are whatever the protocol servers send and receive, each
  measured against pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1` (ADR-0003)
  by its own task.

## Acceptance criteria

- [ ] `VerboseLogEscapingTests.EveryByte_RendersByTheAdr0006Rule` checks all 256 byte
      values against the rule above.
- [ ] Named cases pin exact text: `ESC [ 2 J` renders `\x1B[2J`; `\` renders `\x5C`;
      DEL renders `\x7F`; 0x80 and 0xFF render `\x80` and `\xFF`; `CR LF` renders
      `\r\n`; `GET / HTTP/1.1` renders unchanged.
- [ ] `VerboseLogEscapingTests.Rendering_RoundTripsEveryByte` decodes the rendering of all
      256 bytes, in a test-local decoder, back to the original bytes.
- [ ] A test proves a note whose text holds ESC and a non-ASCII character is written with
      them escaped, and that bytes sent are rendered by the same rule as bytes received.
- [ ] `dotnet build Surl.Output.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Output.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
