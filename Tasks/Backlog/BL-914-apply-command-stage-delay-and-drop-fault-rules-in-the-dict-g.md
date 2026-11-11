---
id: BL-914
title: Apply command-stage delay and drop fault rules in the DICT, Gopher and TELNET servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-901, BL-1035]
touches: [Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitTests, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitTests, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitTests, Documentation/Planning/Decisions/ADR-0138-how-the-dict-gopher-and-telnet-servers-answer-a-chosen-error.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-914 — Apply command-stage delay and drop fault rules in the DICT, Gopher and TELNET servers

## Goal

A fault rule `delay;stage=command;ms=<n>` or `drop;stage=command` that matches a DICT command, a Gopher selector
or a TELNET line makes the server wait `ms` on the injected `TimeProvider` before answering it, or abort the
connection in place of the answer, so a test can make one lookup slow or cut off on purpose.

## Context

- ADR-0124 decision 4's table says command-stage `delay` is applied by "each server at a command" (BL-793 to
  BL-798). BL-798 (ADR-0138) built only the `error` kind: `DictCommandFaults`, `GopherSelectorFaults` and
  `TelnetLineFaults` already ask the plan at the same points and note any other kind as not applied (ADR-0138
  decision 5).
- BL-901 adds the seams this needs to `Surl.Protocol.Abstractions` (pausing the engine's idle clock during a
  delay, aborting from a server) for the mail servers; BL-909 does the same for SSH and SMB. This task applies
  them in the three servers here, and replaces each library's `FormatListenUrl` copy with BL-901's shared one.
- Each application publishes one `FaultAppliedEvent` with the kind applied and the target ADR-0138 decision 6
  names.

## Acceptance criteria

- [ ] Named tests in `DictCommandFaultsTests`, `GopherSelectorFaultsTests` and `TelnetLineFaultsTests`: a
      matching command-stage `delay` holds the answer for `ms` on a `ManualTimeProvider` and then answers as
      usual; a matching command-stage `drop` aborts the connection with nothing written for that command; each
      publishes one `FaultAppliedEvent` of its kind.
- [ ] The tests that pin "a Delay/Drop rule is not applied" are replaced by these, and ADR-0138 decision 5 is
      amended to say so.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the three libraries keep 100% line and
      branch coverage.

## Notes

Filed by BL-798 (dark factory lane 2, 2026-11-05).

- 2026-11-11 (lane 3): added `Documentation/Planning/Decisions/ADR-0138-...md` to `touches` - decision 5
  must be amended and no task in Doing names it. `dotnet build` then failed with "A compatible .NET SDK was
  not found": global.json pins 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed.
  Depends on BL-1035. Uncommitted work left for the shift to stash (unbuilt, untested): the three
  `*Faults` classes now have `FindAndApplyAsync(context, connection, ...)` returning the rule answered in
  place of the command (a Drop, or a fitting Error) or null, through `CommandStageFaults` and
  `ReportedListenUrl.Format` (the `FormatListenUrl` copies are gone); `DictCommandResponder`,
  `GopherProtocolServer` and `TelnetProtocolServer` (drop ends the session, pending outbound discarded)
  use it; the three CLAUDE.md files are updated; `DictCommandFaultsTests` has the delay, drop and
  OutOfSpace tests in place of the Delay-not-applied and FormatListenUrl tests. Still to do: the same
  tests in `GopherSelectorFaultsTests` and `TelnetLineFaultsTests` (TELNET needs a `ManualTimeProvider`
  copy), amend ADR-0138 decisions 5 and 6, build, test and check coverage.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
