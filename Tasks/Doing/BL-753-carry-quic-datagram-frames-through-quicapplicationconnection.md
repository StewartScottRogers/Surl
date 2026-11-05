---
id: BL-753
title: Carry QUIC DATAGRAM frames through QuicApplicationConnection and the flow loop
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-573]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-753 — Carry QUIC DATAGRAM frames through QuicApplicationConnection and the flow loop

## Goal

An `IQuicApplicationProtocol` (HTTP/3 with HTTP datagrams, WebTransport) sends and receives QUIC `DATAGRAM`
frames through `QuicApplicationConnection` while `QuicDatagramFlowServer` drives the connection.

## Context

- ADR-0086 decision 4; RFC 9221 sections 3 to 5.
- BL-573 gave `QuicServerConnection` `SendDatagram`, `QuicConnectionOutput.ReceivedDatagramFrames` and
  `QuicServerConnectionOptions.MaxDatagramFrameSize`. The application never sees the bare connection:
  `QuicApplicationConnection` puts every call behind one lock, and the flow loop (`QuicDatagramFlowServer`) is
  what calls `ReceiveDatagram`, so the frames it returns must be queued for the application to await.
- BL-411 (HTTP datagrams in `Surl.Http3`) depends on this.

## Acceptance criteria

- [ ] Named tests: `QuicApplicationConnection` sends a datagram the test client receives in a `DATAGRAM` frame
      (type 0x31); a `DATAGRAM` frame the test client sends is returned by an awaited receive call; once the
      connection ends a waiting receive ends as `ReadAsync` does and a send throws `OperationCanceledException`;
      sending when the client offered no `max_datagram_frame_size` throws `InvalidOperationException`.
- [ ] Every existing `Surl.Quic.UnitTests` expected byte is unedited.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-573, whose acceptance named `QuicServerConnection` only.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
