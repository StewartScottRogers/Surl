---
id: BL-011
title: Accept TCP connections in Surl.Networking behind the listener seam
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-004, BL-005]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-011 — Accept TCP connections in Surl.Networking behind the listener seam

## Goal

`Surl.Networking.UnitLibrary` binds the address a listen URL names, reports the port
actually bound, and hands each accepted TCP connection to its caller as the connection
type from the listener-seam ADR (BL-000). A bind failure is reported the way that ADR
says, carrying the `SurlExitCode` the exit-code ADR (BL-001) assigns.

## Context

- The listener-seam ADR recorded by BL-000 defines the listener and connection shapes,
  how bind failures are reported, and how socket code meets the coverage gate. Follow
  it. Its `README.md` index entry in `Documentation/Planning/Decisions/` names it.
- The exit-code ADR recorded by BL-001, and `SurlExitCode` after BL-004, give the code
  for each bind failure: address in use, address not local, permission denied.
- `Surl.Networking.UnitLibrary/CLAUDE.md`: the only project that constructs `Socket` or
  `TcpListener`. Keep that code as thin as it can be.
- Hosts: IPv4 literal, bracketed IPv6 literal, and host name (resolved with
  `System.Net.Dns`; every address it resolves to is bound, or the first only, as the
  ADR says). Port 0 binds an ephemeral port, and the bound port is reported back
  because the status line (BL-016) and the conformance tests (BL-020) need it.
- UDP and TLS are not in this task. TLS is BL-012. UDP comes with TFTP later.
- Real-socket tests are `[TestCategory("Integration")]` and bind `127.0.0.1` with port 0
  only, never a fixed port, because lanes and tests run in parallel.

## Acceptance criteria

- [x] A TCP listener type in `Surl.Networking.UnitLibrary` implements the ADR's listener
      shape: start on a listen URL's host and port, report the bound endpoint, accept
      connections as the ADR's connection type until cancelled, and stop.
- [x] The connection adapter over `NetworkStream` implements read, write, half-close
      and abort as the ADR defines them.
- [x] The logic that does not need a socket (host-to-address selection, mapping a
      `SocketException`'s `SocketError` to the exit code) is covered by fast tests with
      no socket. That covers at least `AddressAlreadyInUse`, `AddressNotAvailable` and
      `AccessDenied`, plus an unmapped error.
- [x] `[TestCategory("Integration")]` tests: bind `127.0.0.1:0` and a client
      `TcpClient` exchanges bytes both ways; bind `[::1]:0` when IPv6 loopback is
      available (`Assert.Inconclusive` otherwise); binding a port another listener in
      the test holds reports the address-in-use exit code; cancellation stops accepting.
- [x] Every member not covered by fast tests carries `[ExcludeFromCodeCoverage]` with a
      justifying comment, as the ADR allows. `Measure-CodeQuality.ps1` reports no
      failing member in `Surl.Networking.UnitLibrary`.
- [x] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `dotnet test --filter "FullyQualifiedName~Surl.Networking"`
      (Integration included) is green on Windows.

## Notes

- **ADR-0004 wins over this task's wording.** The Goal and criteria say a bind failure
  carries a `SurlExitCode`; ADR-0004 section 6 and its Consequences say Networking maps
  `SocketError` to `ListenerBindFailure` and `Surl.Core` maps that to the exit code
  (`BindFailed` 45, `CouldNotResolveHost` 6, ADR-0005). The code follows the ADR: tests
  assert `ListenerBindFailure.AddressInUse`, `AddressNotAvailable`, `PermissionDenied`,
  `HostNotFound` and `Other`.
- **Plan (done in-session, no separate architect hand-off; the ADR already fixed the
  shapes).** Public `TcpConnectionListener.StartAsync(ListenUrl, CancellationToken)`
  implements `IConnectionListener`. Pure, fast-tested decisions: `ListenAddressResolver`,
  `ListenerBinder`, `BindFailureClassifier`, `AcceptRace<TAccepted>`, `StreamConnection`
  over an `IConnectionTransportControl`. Socket-touching members sit in
  `TcpConnectionListener` and `SocketTransportControl`, each `[ExcludeFromCodeCoverage]`
  with a comment naming the socket call, and are covered by the Integration tests.
- **Choices (sensible defaults):**
  - Every resolver failure (`HostNotFound`, `TryAgain`, ...) is `HostNotFound`: all of them
    mean the name gave no address to bind.
  - Port 0 with several addresses: when a later address finds the first one's ephemeral
    port taken, everything is released and the bind starts again, up to 5 attempts.
  - IPv6 listening sockets are IPv6-only (`DualMode = false`) on every platform, so
    `[::]` behaves as on Windows everywhere and never also claims IPv4.
  - Accepted sockets get `NoDelay = true`, so small replies are not held by Nagle.
  - An accept that fails with a socket error, or a client that resets before its
    connection is wrapped, surfaces from `AcceptAsync` as `IOException`; the next call
    accepts again.
  - `Abort` sets a zero linger and closes, ignoring a linger option the OS refuses after
    the peer's reset (macOS), because an abort must not throw.
  - No `IListenerFactory` implementation yet: its datagram half belongs to BL-031, and a
    factory that throws for it would not do what its interface says. Filed as BL-055.
  - `ExclusiveAddressUse` is left at the platform default; the empty-host and over-long
    host cases are `Surl.Cli`'s to refuse (BL-013).
- **Review (code-reviewer):** fixed the accept/stop race with a lock, the unguarded
  `CreateConnection`, `ResetAndClose` on a reset peer, ephemeral-port collisions across
  addresses, the IPv6 dual-mode difference, `ObjectDisposedException` from a shutdown
  after a concurrent abort, and a port read failing after a bind; added a server-first
  half-close integration test and an Inconclusive guard on the `localhost` test.
  Lingering close before dispose (unread client bytes turning into RST, curl exit 56)
  filed as BL-056.
- **Conformance stage skipped:** nothing is user-visible yet; no option, exit code or
  wire byte changed until `Surl.Core` and `Surl.Console` use the listener.
- Results: `Surl.Networking.UnitTests` 86 tests (75 fast, 11 Integration), green on
  Windows over repeated runs; `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary`
  100% line, 100% branch, 0 failing members, worst CRAP 4.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. TcpConnectionListener binds a listen URL's addresses on one port, reports the bound port, accepts IConnections over NetworkStream, and reports bind failures as ListenerBindException
