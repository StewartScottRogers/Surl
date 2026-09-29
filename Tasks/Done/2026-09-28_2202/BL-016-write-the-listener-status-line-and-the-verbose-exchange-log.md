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
completed: 2026-09-28
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

- [x] A status-line writer produces the ADR's exact text for a bound listen URL,
      including the actually bound port when the URL asked for port 0. Tests cover an
      IPv4 and a bracketed IPv6 address.
- [x] A verbose-log writer produces the ADR's exact lines for: bytes received, bytes
      sent, a note, and a line that holds a CR or a non-printable byte, rendered as the
      ADR says.
- [x] With verbose off, the verbose-log writer writes nothing, pinned by a test.
- [x] Writes from concurrent exchanges never interleave within a line, pinned by a test
      that writes from several tasks at once.
- [x] `dotnet build Surl.Output.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Output.UnitLibrary`.

## Notes

- Plan (2026-09-28): no new ADR needed; ADR-0007 sections 5, 7 and 8 and ADR-0006
  section 3 fix every byte. Delivered as:
  - `ListenerStatusLine` (writer injected): `Format`, `Write`, and
    `FormatBoundListenUrl`, public so BL-015's `Connection from ... on <listen url>` note
    writes the URL the same way. IPv6 is detected by a `:` in `ListenUrl.Host` (the
    parser strips brackets, and no host name or IPv4 literal holds a colon); a zone's
    `%` is written `%25`. An unbound `ListenUrl` throws `ArgumentException`.
  - `ExchangeLogEscaping`: the ADR-0006 escaped rendering.
  - `VerboseExchangeLogFactory(TextWriter, bool verbose)` implements ADR-0004's
    `IExchangeLogFactory`; verbose off hands out a stateless `SilentExchangeLog`.
    Each event's lines (one call can be several lines) are built first and written in
    one `Write` under a lock shared by the factory's logs, so an event's lines also stay
    together. `remoteEndPoint` is validated but not written: the engine's note names it.
- Line endings: `Environment.NewLine`, as ADR-0007 section 5 already decides; tests
  build expectations from it, so they pass on every platform.
- Concurrency test uses a writer that appends one char at a time and yields between
  chars, so an unsynchronised write would visibly interleave.
- Quality: Surl.Output.UnitLibrary 100% line, 100% branch, 21 members, 0 failing,
  worst CRAP 10. Output projects pass `dotnet format --verify-no-changes`.
- Seen, outside this task's `touches`: solution-wide `dotnet format --verify-no-changes`
  reports ENDOFLINE on `Surl.Cli.UnitLibrary/SchemeDefaultPorts.cs` lines 51-55 in this
  checkout (LF where CR LF is expected). Left alone.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Surl.Output writes the ADR-0007 listener status line and the -v verbose exchange log (escaped, split, never interleaved, silent without -v)
