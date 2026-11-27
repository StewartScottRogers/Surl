---
id: BL-1977
title: Make the Telnet comments and notes true about delay and drop faults and the final line feed
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1977 — Make the Telnet comments and notes true about delay and drop faults and the final line feed

## Goal

The Telnet library's comments and `CLAUDE.md` say what the code does with `delay` and `drop` fault rules, and with
the last line feed in echo-and-close mode.

## Context

Found while working BL-1965 (2026-11-27), read from source. XML doc comments, code comments and project-notes text
only; no code or behaviour change. All files are in `Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary`; line
numbers are as of that date and approximate.

Fault kinds. `TelnetLineFaults.FindAndApplyAsync` applies `delay`, `drop` and a fitting `error` rule (BL-914), but:

- `TelnetLineFaults.cs`, about lines 12-16, the class remarks: only an `Error` rule is applied, any other kind is
  noted as not applied.
- `CLAUDE.md`, "Chosen-error fault" paragraph: "a rule of another kind, is noted and not applied", which the next
  sentences contradict.
- `TelnetProtocolServer.cs`, about lines 46-49, the class remarks: cover chosen errors only.

Final line feed. `TelnetSession.EchoVerbatim` holds each LF back until more data follows, so a final LF is never
echoed, but these say every data byte is echoed verbatim or as it comes:

- `CLAUDE.md`, "Echo-and-close mode" paragraph.
- `TelnetUpstreamTestCaseReplies.cs`, about lines 7-8.
- `TelnetSession.cs`, about lines 43-44, the `echoesVerbatim` parameter comment.

No store, event or fault changes: documentation only.

## Acceptance criteria

- [ ] `TelnetLineFaults`'s class remarks say `delay`, `drop` and a fitting `error` rule are applied, and name what is noted as not applied.
- [ ] `CLAUDE.md` says only a reply that does not fit, or a kind other than `error`, `delay` or `drop`, is noted and not applied.
- [ ] `TelnetProtocolServer`'s class remarks say a `delay` rule holds a completed line's answer and a `drop` rule aborts the connection.
- [ ] `CLAUDE.md`, `TelnetUpstreamTestCaseReplies.cs` and `TelnetSession.cs` say a final LF is not echoed in echo-and-close mode.
- [ ] The diff changes only `CLAUDE.md` and comment lines.
- [ ] `dotnet build Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
