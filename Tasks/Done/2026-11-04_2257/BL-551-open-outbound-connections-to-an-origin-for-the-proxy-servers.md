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
completed: 2026-11-04
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

- [x] The contract and an in-memory implementation exist in `Surl.Protocol.Abstractions`, with
      named tests in `Surl.Protocol.Abstractions.UnitTests` that connect through the in-memory one
      and observe a refused origin and an unreachable one.
- [x] `Surl.Networking` implements the contract; named tests connect to a loopback listener started
      in the test by address and by `localhost`, and observe the failure reported for a closed port,
      an unresolvable name and an origin the allowed-origin rule refuses, each as the ADR names it.
      None needs `TestCategory=Integration`.
- [x] `dotnet build -warnaserror` is clean for both libraries, the fast tests are green, and both
      keep 100% line and branch coverage.

## Notes

- Built ADR-0088 decision 2 as written: a new `IOriginConnector` (not `IDataConnectionOpener`),
  `OriginConnectionException` with `OriginConnectionFailure` (`HostNotFound`, `NotAllowed`,
  `Refused`, `Unreachable`, `TimedOut`), `InMemoryOriginConnector` and `OriginConnectionRequest`
  in `Surl.Protocol.Abstractions`; `SocketOriginConnector` in `Surl.Networking`.
- Choice: the allowed-origin rule is `AllowedOriginRule` in `Surl.Protocol.Abstractions`, not
  `Surl.Networking`, so `Surl.Cli` (BL-555) can build it from `--proxy-allow-origin` without a
  reference to `Surl.Networking`. It is immutable: `LoopbackOnly` (default) or `EveryAddress`
  (`'*'`), `WithRange(network, prefix)`, `WithProxyEndPoint(endPoint)`.
- Choice: "the proxy's own endpoint is never an origin" is enforced by the rule, from the
  listening end points the composition root adds with `WithProxyEndPoint` once the proxy has
  bound. A wildcard listen address (`0.0.0.0`, `::`) refuses its port on every address, the
  conservative reading: any local address on that port may be the proxy itself.
- Choice: the timeout covers name resolution and every connect attempt together; when every
  admitted address fails, the last failure is reported (`Refused` for `ConnectionRefused`,
  `Unreachable` otherwise); a resolution failure of any kind, or no address, is `HostNotFound`.
  The caller's own cancellation stays `OperationCanceledException`, also when the timeout fired
  at the same moment.
- Choice: an origin connection is a `StreamConnection` with no TLS handshake: the proxy passes TLS
  through (decision 4). The connect reuses `SocketDataConnectionSockets.ConnectAsync`.
- `InMemoryOriginConnector` applies no rule: a test scripts a refused origin as `NotAllowed`; an
  unscripted origin is `Refused`, as a port nobody listens on is.
- The loopback tests (`SocketOriginConnectorLoopbackTests`) are fast tests, as the acceptance
  criteria ask, overriding the default "network tests are Integration" rule: every socket is on
  loopback and the one name resolved is `surl-origin.invalid` (RFC 6761).
- Verified: `dotnet build` clean (0 warnings), every fast test project green (Abstractions 377,
  Networking 323), the new types at 100% line and branch coverage in their own libraries.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Proxies reach origins through IOriginConnector: SocketOriginConnector resolves, applies AllowedOriginRule and connects; InMemoryOriginConnector serves proxy tests
