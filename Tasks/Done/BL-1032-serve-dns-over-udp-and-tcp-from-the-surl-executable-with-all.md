---
id: BL-1032
title: Serve dns over UDP and TCP from the surl executable with --allow-zone-transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-405]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary]
requirement: none
created: 2026-11-06
completed: 2026-11-06
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

- [x] A named `Surl.Composition.UnitTests` test finds `dns` registered with a UDP and a TCP listener on one port
      when the switch is on, and absent when it is off.
- [x] A named test shows `--allow-zone-transfer` reaching both servers, and its absence leaving zone transfer off.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, including
      `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` for `--allow-zone-transfer`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

- Delivered directly in this lane, without a separate architect pass: ADR-0092 decisions 1, 4 and 7 already fix
  every choice.
- Composition: `ExtendedProtocolServers.Registrations` gains two `dns` entries, `DnsUdpProtocolServer` and
  `DnsTcpProtocolServer`, both over `ComposedProtocolServices.DnsZones` and `AllowsZoneTransfer` (set from
  `SurlCommandLine.AllowZoneTransfer`); the serving engine already binds a scheme with a datagram and a connection
  server on one port, as it does for https with HTTP/3. `ComposeExtendedSchemes` now de-duplicates, so
  `--version`'s `Extended:` line reads `dns nats natss s3`.
- Zones: `ComposeDnsZoneFolder` (`<data directory>/.surl/dns`, none without `--directory`) and `LoadDnsZonesAsync`
  (`(37) Could not read <file>: <reason>`), loaded last in `LoadServiceStateAsync` and held on the new
  `ServiceState.DnsZones` init property (default: no zone), so no positional `ServiceState` caller changed.
- Choice: zones load with `--directory` whether or not a `dns` listen URL is given, as the LDAP directory does
  (ADR-0072 decision 1's rule, which ADR-0092 decision 1 applies to DNS). A malformed zone file refuses every
  start on that data directory, as a malformed `directory.ldif` does.
- Choice: `--allow-zone-transfer` writes no start warning. ADR-0092 decision 4 likens it to `--list-directories`
  (a deployment choice in `security`, not `testing`), so it joins the security topic's "deployment choices that
  write no warning" and `AiHelpFactsTests.WidensWhatAPeerMayDo_IsExactlyTheSecurityOptionsOutsideTesting`.
- Fix found on the way: `ComposedServing.WriteHttp3Note` fired for any listen URL with a shared-port datagram
  listener, so `surl -v dns://...` would have written `* Serving HTTP/3 over QUIC`. It now fires for `https`
  only (`RunAsync_VerboseDnsWithTheSwitchUnset_BindsTcpThenUdpOnItsPortAndWritesOneStatusLineAndNoHttp3Note`).
- Touches widened: `Application/Surl.Mcp.UnitLibrary`. `CommandLinePathMembersTests` requires every
  `SurlCommandLine` member classified, so `AllowZoneTransfer` joins `CommandLinePathMembers.MembersWithoutPaths`.
  No task in Doing names it (BL-1005 touches only `Documentation/Planning/Decisions`).
- `Surl.Api`'s `SurlServerOptions` has no `AllowZoneTransfer` property yet; a test passes the flag as a raw
  argument. Left out: outside this task's projects, and BL-611's design governs that surface.
- Tests: `ExtendedProtocolServersTests`
  (`Compose_ThisBuild_RegistersDnsAsADatagramAndAConnectionServerSoOneListenUrlBindsUdpAndTcpOnOnePort`,
  `ComposeExtendedProtocolServers_SwitchAtZero_ComposesNoDnsServer`,
  `Compose_AllowsZoneTransferSetOrNot_ReachesBothDnsServersOverTheOneZoneSet`), `SurlServerCompositionTests.DnsZones.cs`,
  and in `CommandLineRunnerExtendedProtocolTests` the TCP-then-UDP bind on one port and the `SURL_EXTENSIONS=0` refusal.
- Coverage (`Measure-CodeQuality.ps1`): line 100% in Surl.Cli, Surl.Composition and Surl.Console; branch 100% in
  Composition and Console, and 99.88% in Cli from `ConfigurationItemReader.ReadHeadValue`. That member and the
  complexity failures the script lists (MailboxFileReader, FaultReplyShapes, ComposeAuthentication,
  MailboxComposition.WriteStartLines) are in code this task did not change.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. surl dns://host:port serves DNS over UDP and TCP on one port from <path>/.surl/dns zones; --allow-zone-transfer, dns aihelp topic, SURL_EXTENSIONS=0 turns it off
