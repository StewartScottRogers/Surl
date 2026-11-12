---
id: BL-1036
title: Let a listen URL name which of its scheme's servers serve it so dns can bind each transport apart
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1035]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Documentation/Planning/Decisions/ADR-0092-how-the-dns-server-answers-dns-and-its-encrypted-transports.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1036 — Let a listen URL name which of its scheme's servers serve it so dns can bind each transport apart

## Goal

The serving engine binds, for each listen URL, only the listeners that listen URL asks for - its connection
listener, its datagram listener, or both on one port - instead of always binding every server its scheme has, so
one `dns` listen URL can serve UDP alone while another in the same process serves TCP with TLS from the first byte.

## Context

- Found by BL-1017 (`--dns-transport udp,tcp,tls`, ADR-0092 decision 5) on 2026-11-11. Today
  `ServingEngine.MapServersByScheme` (`Foundation/Surl.Core.UnitLibrary/ServingEngine.cs`) maps a scheme to one
  connection server and at most one shared-port datagram server, and `StartListenersAsync` binds both on every
  listen URL of a scheme that has both (ADR-0082 decision 6: `https` with HTTP/3). `dns` has both
  (`DnsUdpProtocolServer`, `DnsTcpProtocolServer`, BL-1032), so a DoT listen URL (`dns`,
  `IsImplicitTls = true`, port 853) would also bind UDP on 853, and `--dns-transport udp,tls` cannot leave TCP off
  the plain listen URL while keeping it on the DoT one.
- Shape to start from: an init-only member on `ListenUrl`
  (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/ListenUrl.cs`) naming which of its scheme's listeners bind
  - connections, datagrams, or (unset, the default) every one the scheme's servers take - as `IsImplicitTls` and
  `IsProxy` are per-listen-URL members (BL-1020, ADR-0088 Amendment 2). Unset keeps today's behaviour exactly,
  so `https` with `--http3` still shares one port. A listen URL asking for a listener its scheme has no server
  for is refused before any listener binds, as a scheme with no server is today.
- BL-1018 (DoH: a second connection server for `dns`) and BL-1019 (DoQ: a second datagram server for `dns`) will
  need the engine to pick between servers of one scheme too. Decide whether this member should also name the
  server (for example a transport label the registrations carry) so those tasks need no second contract change,
  and record the choice in an ADR amendment to ADR-0092 decision 5 or ADR-0082 decision 6.
- No static mutable state; many engines side by side (root `CLAUDE.md`, "Surl is a stand-in for tests").
- Stand-in: no store, no new event; a test observes which listeners the in-memory listener factory was asked for.

## Acceptance criteria

- [x] A named `Surl.Protocol.Abstractions.UnitTests` test pins the new member's default and that it is part of
      the listen URL's equality, as `IsImplicitTls` is.
- [x] A named `Surl.Core.UnitTests` test shows a scheme with a connection and a datagram server binding only the
      datagram listener on a listen URL that asks for datagrams, only the connection listener on one that asks for
      connections, and the shared-port pair on one that leaves it unset.
- [x] A named `Surl.Core.UnitTests` test shows a listen URL asking for a listener its scheme has no server for
      refused before any listener binds.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- 2026-11-11, lane 6: `dotnet --list-sdks` lists only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100; `global.json`
  pins 10.0.401 with `rollForward: latestFeature`, so `dotnet` finds no compatible SDK and no build or test runs.
  No code written. Parked behind BL-1035 (Stewart reinstalls the SDK), as BL-985 and BL-1007 are.
- 2026-11-11, lane 1: `ListenUrl.OnlyTransport` (`ListenTransport?`: `Connections`, `Datagrams`, null = every
  listener, the old behaviour). `ServingEngine.FindServers` now resolves each listen URL to a private
  `ListenUrlServers` pair filtered by it; both set binds the shared-port pair, one set binds that listener alone,
  none set is `UnsupportedProtocol` before any bind. Decision (ADR-0092 Amendment 1, decision 5): the member names a
  transport, not a server; BL-1018/BL-1019 either dispatch inside one server per transport (by `IsImplicitTls` and
  the agreed ALPN) or add one more init-only member, both additive. Added the ADR-0092 file to `touches` to record
  the amendment; no task in Doing names it (BL-1034 touches only WebTransport).
- Tests: `ListenUrlTests.OnlyTransport_Unset_IsNullSoEveryListenerOfTheSchemeBinds`,
  `ListenUrlTests.OnlyTransport_Set_IsPartOfEqualityAndSurvivesTheBoundPort`,
  `ServingEngineTests.ServeAsync_OnlyTransportOnEachListenUrl_BindsOnlyTheListenersEachAsksFor`,
  `ServingEngineTests.ServeAsync_ListenUrlAskingForConnectionsOfADatagramOnlyScheme_IsUnsupportedBeforeAnyListenerBinds`
  and `..._ListenUrlAskingForDatagramsOfAConnectionOnlyScheme_...`. `Measure-CodeQuality.ps1 -Library` reports
  both libraries 100% line, 100% branch, 0 failing members.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so no build or test runs
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. A listen URL's OnlyTransport binds only its connection or datagram listener, so one dns URL can serve UDP alone beside another serving TCP
