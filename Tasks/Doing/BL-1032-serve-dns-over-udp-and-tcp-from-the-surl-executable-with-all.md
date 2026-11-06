---
id: BL-1032
title: Serve dns over UDP and TCP from the surl executable with --allow-zone-transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-405]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1032 — Serve dns over UDP and TCP from the surl executable with --allow-zone-transfer

## Goal

`surl dns://127.0.0.1:0` answers DNS over UDP and TCP on the same port from the zones under the data directory
(ADR-0092 decisions 1 and 4), `--allow-zone-transfer` turns on AXFR and IXFR, the `dns` `--aihelp` topic and help
category exist, and the extension switch (`SURL_EXTENSIONS=0`) turns it off.

## Context

- First of the four tasks BL-485 ("Serve dns and its encrypted transports from the surl executable") was split
  into on 2026-11-06 for the 60-minute size limit: this (UDP and TCP), BL-1017 (`--dns-transport` and DNS over
  TLS), BL-1018 (DNS over HTTPS), BL-1019 (DNS over QUIC).
- Decision: ADR-0092 decision 1 (zones are master files under the data directory, no option names the folder),
  decision 4 (`--allow-zone-transfer`, off by default) and decision 7 (`--aihelp`, help and composition).
- Composition: extended servers register in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`, beside `s3`; BL-609,
  BL-804), with a project reference to `Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary`. One listen
  URL binds a UDP and a TCP listener on the same port (`DnsUdpProtocolServer`, `DnsTcpProtocolServer`, both
  taking `allowZoneTransfer`); the zones load through `DnsZoneFolder` after the data-directory lock, as the mail
  store is loaded (`ServiceState`).
- `--allow-zone-transfer`: no argument, categories as ADR-0092 decision 7 names, its `OptionArgumentType` and
  `--aihelp` facts (ADR-0034, ADR-0046), and a start warning if ADR-0092 calls it a loosening option.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with `dns`, an `AiHelpProse.TopicAbout`
  paragraph naming RFC 1035 and the transport RFCs as its oracle, an `AiHelpExamples` entry, and the topic list in
  `AiHelpTextTests` growing by one.
- Stand-in: the zones are read-only files in the data directory, so a test seeds them by writing zone files before
  start; no shared store and no new events beyond the engine's exchange events.

## Acceptance criteria

- [ ] A named `Surl.Composition.UnitTests` test finds `dns` registered with a UDP and a TCP listener on one port
      when the switch is on, and absent when it is off.
- [ ] A named test shows `--allow-zone-transfer` reaching both servers, and its absence leaving zone transfer off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, including
      `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` for `--allow-zone-transfer`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
