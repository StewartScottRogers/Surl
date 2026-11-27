---
id: BL-1965
title: Name Surl.Composition as the composer in the Telnet server's comments and notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary/TelnetProtocolServer.cs, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1965 — Name Surl.Composition as the composer in the Telnet server's comments and notes

## Goal

The Telnet library's constructor comment and `CLAUDE.md` name `Surl.Composition` as the project that composes
`TelnetProtocolServer`.

## Context

Found while working BL-1949 (2026-11-27), read from source. XML doc comments and project-notes text only; no code or
behaviour change. Line numbers are as of that date and approximate. In
`Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary`:

- `TelnetProtocolServer.cs`, about line 67, the parameterless constructor: "Creates the server as `Surl.Console`
  composes it".
- `CLAUDE.md`, about line 26: the echo-and-close mode is "Never composed by `Surl.Console`". That is true, but the
  project it needs to name is the one that composes servers.

The code: `ProtocolServerRegistrations.Original` in `Application/Surl.Composition.UnitLibrary` registers
`new TelnetProtocolServer()` for `telnet`. Only `Conformance/Surl.Conformance.UnitTests/UpstreamTestCaseServerHalves.cs`
and the Telnet unit tests construct it with `TelnetUpstreamTestCaseReplies`. The library does not reference
`Surl.Composition`, so name it in `<c>`.

No store, event or fault changes: documentation only.

## Acceptance criteria

- [x] The parameterless constructor's summary says `Surl.Composition` composes the server that way: banner, CRLF echo and `quit`.
- [x] `CLAUDE.md` says `Surl.Composition` never composes the echo-and-close mode.
- [x] No comment or `CLAUDE.md` line in the library says `Surl.Console` composes the server.
- [x] The diff changes only `CLAUDE.md` and `///` comment lines of `TelnetProtocolServer.cs`.
- [x] `dotnet build Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary -warnaserror` is clean.

## Notes

- Two lines changed. Checked against `ProtocolServerRegistrations.Original`, which registers
  `new TelnetProtocolServer()` for `telnet`.
- 2026-11-27: full `dotnet build` clean (0 warnings), format check of `TelnetProtocolServer.cs` clean, fast tests
  green.
- Filed BL-1983 for other stale Telnet comments (delay and drop faults, the held-back final LF), directly with the
  board script.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Telnet constructor comment and CLAUDE.md name Surl.Composition as the composer
