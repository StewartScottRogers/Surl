---
id: BL-381
title: Implement the QUIC datagram contract over UDP in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-380]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Fake-socket tests show datagrams from two remote endpoints delivered to two separate
      connections, a datagram past `--max-connections-per-address` refused as ADR-0028 says, and
      disposal releasing the socket.
- [ ] A test binding `127.0.0.1:0` reports the bound port through the listener's `BoundEndPoints`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
