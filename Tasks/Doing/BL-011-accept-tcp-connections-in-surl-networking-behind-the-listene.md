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
completed:
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

- [ ] A TCP listener type in `Surl.Networking.UnitLibrary` implements the ADR's listener
      shape: start on a listen URL's host and port, report the bound endpoint, accept
      connections as the ADR's connection type until cancelled, and stop.
- [ ] The connection adapter over `NetworkStream` implements read, write, half-close
      and abort as the ADR defines them.
- [ ] The logic that does not need a socket (host-to-address selection, mapping a
      `SocketException`'s `SocketError` to the exit code) is covered by fast tests with
      no socket. That covers at least `AddressAlreadyInUse`, `AddressNotAvailable` and
      `AccessDenied`, plus an unmapped error.
- [ ] `[TestCategory("Integration")]` tests: bind `127.0.0.1:0` and a client
      `TcpClient` exchanges bytes both ways; bind `[::1]:0` when IPv6 loopback is
      available (`Assert.Inconclusive` otherwise); binding a port another listener in
      the test holds reports the address-in-use exit code; cancellation stops accepting.
- [ ] Every member not covered by fast tests carries `[ExcludeFromCodeCoverage]` with a
      justifying comment, as the ADR allows. `Measure-CodeQuality.ps1` reports no
      failing member in `Surl.Networking.UnitLibrary`.
- [ ] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `dotnet test --filter "FullyQualifiedName~Surl.Networking"`
      (Integration included) is green on Windows.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
