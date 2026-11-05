---
id: BL-381
title: Implement the QUIC datagram contract over UDP in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-380]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-381 — Implement the QUIC datagram contract over UDP in Surl.Networking

## Goal

`Surl.Networking` implements BL-380's QUIC datagram contract over a real UDP socket. A listener binds
the address and port its listen URL names, and hands each QUIC connection's datagrams to the server,
within the connection and per-address limits of ADR-0006.

## Context

- Decision: BL-372's ADR, decisions 5 and 6 (the UDP port, and whether it is the same port as the TCP
  `https` listener).
- Existing UDP code to build on: `UdpDatagramListener`, `UdpDatagramSocket`, `DatagramDemultiplexer`,
  `DemultiplexedDatagramFlow` and `IDatagramSocket` in `Surl.Networking.UnitLibrary`, which TFTP uses.
  Only `Surl.Networking` constructs a `Socket` or `UdpClient` (root `CLAUDE.md`).
- Limits: ADR-0006 section 1 (`--max-connections`, `--max-connections-per-address`) and ADR-0028
  (a refusal past a limit is noted outside any exchange).
- Tests of socket code run through `IDatagramSocket` fakes, with no network, as the existing
  `Surl.Networking.UnitTests` do.

## Acceptance criteria

- [x] Fake-socket tests show datagrams from two remote endpoints delivered to two separate
      connections, a datagram past `--max-connections-per-address` refused as ADR-0028 says, and
      disposal releasing the socket.
- [x] A test binding `127.0.0.1:0` reports the bound port through the listener's `BoundEndPoints`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Plan (2026-11-04). ADR-0082 decision 5 rides each QUIC connection on one `IDatagramFlow` per remote
  endpoint, which `UdpDatagramListener` and `DatagramDemultiplexer` already provide, and leaves the
  ADR-0006 limits to `Surl.Core`'s serving engine. The engine already refuses a flow past them through
  `IDatagramRefusalWriter` and notes the refusal outside any exchange (ADR-0028). So the listener needed
  no change. The missing piece was decision 6's port rule, now `SharedPortListenerPair`: TCP binds
  first, then UDP binds the port TCP got. With port 0, a taken UDP port releases both and the pair is
  tried again, up to 10 attempts in all. "Tried again, up to 10 times" is read as 10 attempts, the
  first included, because that is the simpler reading.
- `ListenUrl.WithBoundPort` keeps `Port` at 0 and sets `BoundPort`. So the UDP listener is started
  with a listen URL whose `Port` is the shared port, and its `ListenUrl` differs from the TCP
  listener's in `Port`. BL-384's status line should read the TCP listener's listen URL.
- `UdpDatagramListenerConnectionLimitTests` show the refusal end to end: they run `ServingEngine`
  over this listener on fake sockets. That needed a test-only reference from
  `Surl.Networking.UnitTests` to `Surl.Core.UnitLibrary`. It is inside this task's `touches`, and
  Core does not reference Networking, so there is no cycle.
- BL-384 wires the pair into the executable and into the engine's listener start.
- Tests: `SharedPortListenerPairTests` (8, fast), `UdpDatagramListenerConnectionLimitTests` (3, fast),
  `SharedPortListenerPairIntegrationTests` (2, Integration). Networking is at 100% line and 100%
  branch coverage, worst CRAP 10 (`Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary`).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. HTTP/3's UDP listener binds beside TCP on one port, QUIC flows per endpoint within the connection limits
