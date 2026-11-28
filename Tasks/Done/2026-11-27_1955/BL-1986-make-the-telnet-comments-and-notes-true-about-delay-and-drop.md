---
id: BL-1986
title: Make the Telnet comments and notes true about delay and drop faults and the final line feed
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1986 — Make the Telnet comments and notes true about delay and drop faults and the final line feed

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

- [x] `TelnetLineFaults`'s class remarks say `delay`, `drop` and a fitting `error` rule are applied, and name what is noted as not applied.
- [x] `CLAUDE.md` says only a reply that does not fit, or a kind other than `error`, `delay` or `drop`, is noted and not applied.
- [x] `TelnetProtocolServer`'s class remarks say a `delay` rule holds a completed line's answer and a `drop` rule aborts the connection.
- [x] `CLAUDE.md`, `TelnetUpstreamTestCaseReplies.cs` and `TelnetSession.cs` say a final LF is not echoed in echo-and-close mode.
- [x] The diff changes only `CLAUDE.md` and comment lines.
- [x] `dotnet build Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary -warnaserror` is clean.

## Notes

- Both defects matched the source. Also corrected in the same library: `TelnetSession.IsOver` omitted the drop rule,
  and the fault point was described as naming the scheme only, though it also sets the listen URL, so a
  listener-scoped rule matches it.
- Verify, 2026-11-27: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests
  30380 passed, 0 failed, 3 skipped. No integration-level code changed.
- Follow-ups filed: BL-2008 (delay and drop log notes begin with a space when the point has no command), BL-2009
  (ADR-0138 says the TELNET point holds the scheme and nothing else).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Telnet comments and CLAUDE.md now state what delay and drop faults do and that a final LF is never echoed
