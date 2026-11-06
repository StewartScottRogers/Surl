---
id: BL-912
title: Apply command-stage delay and drop fault rules in the DICT, Gopher and TELNET servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-901]
touches: [Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitTests, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitTests, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-912 — Apply command-stage delay and drop fault rules in the DICT, Gopher and TELNET servers

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

## Log

- 2026-11-05: Created.
