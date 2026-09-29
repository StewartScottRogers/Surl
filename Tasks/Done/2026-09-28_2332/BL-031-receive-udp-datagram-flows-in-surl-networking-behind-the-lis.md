---
id: BL-031
title: Receive UDP datagram flows in Surl.Networking behind the listener seam
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-011]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-031 — Receive UDP datagram flows in Surl.Networking behind the listener seam

## Goal

`Surl.Networking.UnitLibrary` binds a UDP listen address and hands each new remote peer's
first datagram to its caller as the datagram-flow type from the listener-seam ADR
(BL-000). Each flow can reply from a fresh port, which TFTP needs.

## Context

- The listener-seam ADR recorded by BL-000 defines the datagram-flow shape (types added
  by BL-005), how bind failures are reported, and how socket code meets the coverage
  gate. Its `README.md` index entry in `Documentation/Planning/Decisions/` names it.
- TFTP, the only datagram protocol upstream curl speaks, requires the server to answer
  each transfer from a new port (RFC 1350, section 4). If the ADR's datagram-flow shape
  cannot express a reply from a new port, stop. Have `task-planner` file a `docs` task
  to extend the ADR, make this task depend on it, and move this task to `Blocked`.
- Builds on BL-011's TCP work: reuse its host-to-address selection and its bind-error
  mapping to `SurlExitCode`.
- Only this project constructs `UdpClient` or a UDP `Socket`. Real-socket tests are
  `[TestCategory("Integration")]` on `127.0.0.1` with port 0.

## Acceptance criteria

- [x] A UDP listener type in `Surl.Networking.UnitLibrary` implements the ADR's
      datagram listener: bind, report the bound endpoint, yield one flow per new remote
      endpoint carrying its first datagram, and stop on cancellation.
- [x] A flow receives later datagrams from its peer, sends replies, and can switch to
      replying from a newly bound ephemeral port, as the ADR defines.
- [x] Logic that needs no socket (demultiplexing datagrams by remote endpoint, flow
      lifetime) is covered by fast tests.
- [x] `[TestCategory("Integration")]` tests on `127.0.0.1:0`: a `UdpClient` in the test
      sends a datagram and receives the reply, including a reply from a new port; two
      peers get two flows.
- [x] Members not covered by fast tests carry `[ExcludeFromCodeCoverage]` with a
      justifying comment, as the ADR allows. `Measure-CodeQuality.ps1` reports no
      failing member in `Surl.Networking.UnitLibrary`.
- [x] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, and the fast
      tests are green.

## Notes

- ADR-0004's `IDatagramFlow` already has `MoveToNewLocalPortAsync`, so no ADR extension was
  needed. Delivered: `UdpDatagramListener` (public, `StartAsync` like the TCP listener, reusing
  `ListenAddressResolver` and `ListenerBinder`), `DatagramDemultiplexer`,
  `DemultiplexedDatagramFlow`, `IDatagramSocket`/`ReceivedDatagram`, `DatagramReceiving`, and
  `UdpDatagramSocket` (the only socket code). `IListenerFactory` itself is not implemented
  here: no Networking type implements it yet for TCP either, and composing it belongs to the
  engine wiring.
- Choices taken as sensible defaults (implementation detail inside ADR-0004's contract, so
  recorded here, not in a new ADR):
  - Flows are keyed by (listen socket, remote endpoint).
  - After a flow moves, what its peer still sends to the listen port (a retransmitted `RRQ`)
    is dropped and opens no new flow while the flow is open: ADR-0004 opens a flow only for
    "a remote endpoint that has no open flow". After the flow is disposed, the peer's next
    datagram opens a new one.
  - Moving twice binds another port and releases the previous one.
  - Bounded queues, 64 each: opened flows waiting for `AcceptFlowAsync`, and datagrams in a
    flow's listen-port inbox. Beyond them a datagram is dropped, as UDP drops it, so a flood
    of spoofed peers cannot grow memory without limit (ADR-0006's spirit).
  - Disposing the listener stops new flows and disposes unaccepted ones, but the listen
    sockets stay open until every handed-out flow still on the listen port has moved or been
    disposed, so "flows already handed out are unaffected" holds for an unmoved flow too.
  - `SocketError.ConnectionReset` on a UDP receive (Windows' report of an ICMP
    port-unreachable after a send to a departed client) is skipped; any other listen-socket
    receive failure ends `AcceptFlowAsync` with `IOException`; flow send, receive and bind
    failures are `IOException`, as on a connection.
- Tests: 28 fast tests in `UdpDatagramListenerFastTests` over `FakeDatagramSocket`; 5
  integration tests in `UdpDatagramListenerTests` on `127.0.0.1:0` and `[::1]:0` (listen-port
  exchange, reply from a new port and receive on it, two peers two flows).
  `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary`: 0 failing members.
- `dotnet format --verify-no-changes` over the whole solution reports end-of-line errors in
  `Surl.Cli.UnitLibrary\SchemeDefaultPorts.cs`, outside this task's `touches`; the Networking
  projects verify clean.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Surl.Networking listens for UDP on a listen URL and hands out one IDatagramFlow per new peer, which can reply from a fresh ephemeral port
