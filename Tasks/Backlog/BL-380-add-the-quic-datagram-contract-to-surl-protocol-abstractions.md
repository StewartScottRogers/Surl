---
id: BL-380
title: Add the QUIC datagram contract to Surl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-372]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] The contract the ADR decides exists, with XML documentation in the shape of the neighbouring
      interfaces, and its in-memory implementation has tests in `Surl.Protocol.Abstractions.UnitTests`.
- [ ] `ProtocolIsolationTests` still pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
