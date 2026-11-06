---
id: BL-485
title: Serve dns and its encrypted transports from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-483, BL-484, BL-405, BL-384]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-485 — Serve dns and its encrypted transports from the surl executable

## Goal

`surl dns://127.0.0.1:0` answers over UDP and TCP on the same port from the zones given. DoT, DoH and DoQ
listeners start as BL-478's ADR decides, DoT with ALPN `dot`. The `--aihelp` topic and help category exist,
and the extension switch turns it off.

## Context

- Decision: BL-478's ADR, decisions 1 and 5 (the zone option, and how encrypted listeners are asked for).
- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`. One
  listen URL binds UDP and TCP; the existing listeners handle each (`Surl.Networking`). QUIC listeners are
  composed as BL-384 composes HTTP/3.
- DoT ALPN `dot` (RFC 7858 section 3.2, RFC 8310). If the per-scheme ALPN list lives in `Surl.Networking`, add
  that project to this task's `touches` before claiming, or file it separately.
- Each new option needs its `OptionArgumentType` and categories (ADR-0034; root `CLAUDE.md`, "`surl --aihelp`").
- `--aihelp`: a `HelpCategories` row with `dns`, an `AiHelpProse.TopicAbout` paragraph naming RFC 1035 and the
  transport RFCs as its oracle, an `AiHelpExamples` entry, and the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `dns` registered when the switch is on, and absent when it is off. Named tests show each
      encrypted-transport option composing its listener.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, including
      `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` for the new options.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 8): BL-609 moved the composition. Extended servers are now registered in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`), not in
  `CommandLineRunner.cs`, so the `dns` registration, its project reference to
  `Surl.Extended.Protocol.DnsQuery.UnitLibrary` and its switch-on/off tests belong in Surl.Composition and its
  tests; both are added to `touches`. BL-803 (Doing) touches Surl.Composition, so the task waits in Backlog
  until that task no longer overlaps.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary, which BL-803 (Doing) touches
