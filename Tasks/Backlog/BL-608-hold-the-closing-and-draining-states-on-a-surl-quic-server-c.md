---
id: BL-608
title: Hold the closing and draining states on a Surl.Quic server connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-377, BL-379]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-608 — Hold the closing and draining states on a Surl.Quic server connection

## Goal

After it sends `CONNECTION_CLOSE`, a `QuicServerConnection` stays in the closing state for three
times the probe timeout and answers packets that arrive then with its `CONNECTION_CLOSE` again, at a
limited rate; after the client's `CONNECTION_CLOSE` it drains for the same time (RFC 9000 section 10.2).

## Context

- BL-377 ends a connection the moment it sends or receives `CONNECTION_CLOSE`: `IsClosed` is true and
  every later datagram is dropped, so a lost `CONNECTION_CLOSE` leaves the client to its idle
  timeout. The probe timeout comes from BL-379's loss recovery.
- Start at `SendConnectionClose` and `Discard` in `Protocols/Http/Surl.Quic.UnitLibrary/QuicServerConnection.cs`.

## Acceptance criteria

- [ ] Named tests, with a fake `TimeProvider`: a packet arriving in the closing state is answered with
      the same `CONNECTION_CLOSE`; past three probe timeouts nothing is answered and `NextDeadline` is
      null; in the draining state nothing is sent.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
