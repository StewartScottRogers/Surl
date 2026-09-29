---
id: BL-016
title: Write the listener status line and the verbose exchange log in Surl.Output
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-003, BL-005]
touches: [Surl.Output.UnitLibrary, Surl.Output.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-016 — Write the listener status line and the verbose exchange log in Surl.Output

## Goal

`Surl.Output.UnitLibrary` writes, to injected `TextWriter`s, the listener status line and
the `-v` verbose exchange log, each in the exact format the command-line ADR (BL-003)
specifies.

## Context

- The command-line ADR recorded by BL-003 under `Documentation/Planning/Decisions/`
  gives the exact status-line text and the verbose-log format, with its line markers for
  bytes received, bytes sent and notes. Its `README.md` index names it.
- The exchange events come from the exchange context defined by the listener-seam ADR
  recorded by BL-000 (types added by BL-005). This library implements the receiving end
  of whatever reporting shape that ADR chose.
- `Surl.Output.UnitLibrary/CLAUDE.md`: never write to the console directly. Write to
  injected writers.
- Line endings: decide in the `/feature` plan whether the log uses `\n` or
  `Environment.NewLine`, state it in the XML doc, and pin it by a test that passes on
  Windows, Linux and macOS.
- `--trace` and `-w` style output are later tasks.

## Acceptance criteria

- [ ] A status-line writer produces the ADR's exact text for a bound listen URL,
      including the actually bound port when the URL asked for port 0. Tests cover an
      IPv4 and a bracketed IPv6 address.
- [ ] A verbose-log writer produces the ADR's exact lines for: bytes received, bytes
      sent, a note, and a line that holds a CR or a non-printable byte, rendered as the
      ADR says.
- [ ] With verbose off, the verbose-log writer writes nothing, pinned by a test.
- [ ] Writes from concurrent exchanges never interleave within a line, pinned by a test
      that writes from several tasks at once.
- [ ] `dotnet build Surl.Output.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Output.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
