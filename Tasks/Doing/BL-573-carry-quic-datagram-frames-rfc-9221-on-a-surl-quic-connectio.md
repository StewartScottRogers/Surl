---
id: BL-573
title: Carry QUIC DATAGRAM frames (RFC 9221) on a Surl.Quic connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-572]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-573 — Carry QUIC DATAGRAM frames (RFC 9221) on a Surl.Quic connection

## Goal

A `Surl.Quic` server connection whose caller sets `max_datagram_frame_size` sends that transport parameter and
receives and sends `DATAGRAM` frames through `QuicServerConnection`; a connection whose caller does not is
unchanged.

## Context

- Decision: ADR-0086 decision 4 ("QUIC DATAGRAM frames"). ADR-0082 decision 4's "the datagram extension is
  not offered" stays true for the HTTP server, which never sets the parameter.
- RFC 9221: section 3 (the `max_datagram_frame_size` transport parameter, 0x20; a `DATAGRAM` frame received
  without it is `PROTOCOL_VIOLATION`; one larger than advertised likewise), section 4 (frame types 0x30 and
  0x31, with and without a length), section 5 (ack-eliciting, congestion controlled, never retransmitted).
- Builds on BL-572's `QuicServerConnectionOptions` and BL-379's loss detection and congestion control.
- BL-411 (HTTP datagrams in `Surl.Http3`) depends on this.

## Acceptance criteria

- [ ] Named tests, with bytes written from RFC 9221: the parameter sent when the options set it and absent
      when they do not; a `DATAGRAM` frame of each type received and delivered; a datagram sent in a
      `DATAGRAM` frame; a lost datagram not retransmitted but counted by congestion control; a `DATAGRAM`
      frame without the parameter, or over the advertised size, ending the connection with
      `PROTOCOL_VIOLATION`.
- [ ] Every existing `Surl.Quic.UnitTests` expected byte is unedited.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-402 (ADR-0086 decision 4).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
