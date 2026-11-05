---
id: BL-380
title: Add the QUIC datagram contract to Surl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-372]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-380 — Add the QUIC datagram contract to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions` holds the contract through which an HTTP/3 server receives QUIC datagrams,
as BL-372's ADR decision 5 decides, with an in-memory implementation that tests drive without a
network.

## Context

- Decision: BL-372's ADR, decision 5. The ADR may reuse `IDatagramListener`, `IDatagramFlow` (one flow
  per remote endpoint, with `FirstDatagram`, `ReceiveAsync`, `SendAsync` and
  `MoveToNewLocalPortAsync`) and `IDatagramProtocolServer` (ADR-0004), or add a contract next to
  them. If it reuses them unchanged and needs no in-memory addition, record that in Notes and move
  this task to `Done` with every box ticked and the ADR as the reason.
- Rule 2 of the overview's architecture: protocol servers never construct a socket and receive their
  transport from the seam. The existing in-memory doubles to copy are `InMemoryConnection` and
  `InMemoryDataConnections`.
- This changes a shared contract, so it touches `Surl.Protocol.Abstractions.UnitLibrary` and runs
  apart from every protocol task, as the task-board skill intends.

## Acceptance criteria

- [x] The contract the ADR decides exists, with XML documentation in the shape of the neighbouring
      interfaces, and its in-memory implementation has tests in `Surl.Protocol.Abstractions.UnitTests`.
- [x] `ProtocolIsolationTests` still pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- ADR-0082 decision 5 keeps `IDatagramListener`, `IDatagramFlow` and `IDatagramProtocolServer`
  unchanged; this task adds only `InMemoryDatagramFlow`, beside `InMemoryConnection`. No new ADR:
  ADR-0082 already decides it.
- Defaults chosen (Claude): local endpoint 127.0.0.1:443 (the HTTPS port the UDP listener shares),
  remote 127.0.0.1:50000 as `InMemoryConnection`, and `MoveToNewLocalPortAsync` moves to
  127.0.0.1:50001 unless a test names another (QUIC never calls it; it is implemented, not refused,
  so the double honours the whole contract).
- The client's datagrams are an unbounded `System.Threading.Channels` channel, so a test can
  `DeliverFromClient` while the server runs and answer what it sent; past the end a receive waits
  until cancelled or disposed (a QUIC server's idle timer then decides), unlike TFTP's
  `ScriptedDatagramFlow`, which advances a fake clock. Sends are locked because a QUIC server's
  receive loop and its timers send from different tasks.
- Verified: `dotnet build -warnaserror` clean, every fast test green (Abstractions 331, including
  `ProtocolIsolationTests` and 18 new `InMemoryDatagramFlowTests`), `InMemoryDatagramFlow` at 100%
  line and branch coverage with the solution's own coverage collector.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. InMemoryDatagramFlow lets tests drive a datagram server (HTTP/3 over QUIC) with no network; ADR-0082 decision 5 keeps the datagram contract unchanged
