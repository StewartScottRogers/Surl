---
id: BL-1036
title: Let a listen URL name which of its scheme's servers serve it so dns can bind each transport apart
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-11
completed:
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

- [ ] A named `Surl.Protocol.Abstractions.UnitTests` test pins the new member's default and that it is part of
      the listen URL's equality, as `IsImplicitTls` is.
- [ ] A named `Surl.Core.UnitTests` test shows a scheme with a connection and a datagram server binding only the
      datagram listener on a listen URL that asks for datagrams, only the connection listener on one that asks for
      connections, and the shared-port pair on one that leaves it unset.
- [ ] A named `Surl.Core.UnitTests` test shows a listen URL asking for a listener its scheme has no server for
      refused before any listener binds.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-11: Created.
