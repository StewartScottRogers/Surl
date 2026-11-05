---
id: BL-551
title: Open outbound connections to an origin for the proxy servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-550]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-551 — Open outbound connections to an origin for the proxy servers

## Goal

A proxy server can ask the listener seam for a connection to an origin, named by host name or
address and port, and receives an `IConnection` without constructing a socket; `Surl.Networking`
implements it, and an in-memory implementation lets proxy tests run without a network.

## Context

- Decision: BL-550's ADR, decision 2 (the contract's name and shape, which origins are allowed, and
  how a refusal or failure is reported). Planning name `IOriginConnector`. If the ADR reuses
  `IDataConnectionOpener` instead, this task adds only what it lacks (host-name resolution, the
  allowed-origin check) and says so in Notes.
- Both proxy families use it: the HTTP proxy (BL-552) and the SOCKS server (BL-558, BL-559; SOCKS4a
  and SOCKS5h hand the proxy a host name to resolve).
- Code: `Foundation/Surl.Protocol.Abstractions.UnitLibrary` (`IDataConnectionOpener`,
  `InMemoryConnection`, `InMemoryDataConnections`, `DataConnectionFailure`) and
  `Foundation/Surl.Networking.UnitLibrary` (`SocketDataConnectionOpener`, `StreamConnection`).
  Only `Surl.Networking` constructs the `Socket`; DNS resolution is `System.Net.Dns`.
- Upstream test cases this serves, once their proxies exist: the `http-proxy`, `https-proxy`,
  `socks4`, `socks5` and `socks5unix` cases listed in BL-556 and BL-562.

## Acceptance criteria

- [ ] The contract and an in-memory implementation exist in `Surl.Protocol.Abstractions`, with
      named tests in `Surl.Protocol.Abstractions.UnitTests` that connect through the in-memory one
      and observe a refused origin and an unreachable one.
- [ ] `Surl.Networking` implements the contract; named tests connect to a loopback listener started
      in the test by address and by `localhost`, and observe the failure reported for a closed port,
      an unresolvable name and an origin the allowed-origin rule refuses, each as the ADR names it.
      None needs `TestCategory=Integration`.
- [ ] `dotnet build -warnaserror` is clean for both libraries, the fast tests are green, and both
      keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
