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
completed: 2026-11-05
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

- [x] Named tests, with bytes written from RFC 9221: the parameter sent when the options set it and absent
      when they do not; a `DATAGRAM` frame of each type received and delivered; a datagram sent in a
      `DATAGRAM` frame; a lost datagram not retransmitted but counted by congestion control; a `DATAGRAM`
      frame without the parameter, or over the advertised size, ending the connection with
      `PROTOCOL_VIOLATION`.
- [x] Every existing `Surl.Quic.UnitTests` expected byte is unedited.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-402 (ADR-0086 decision 4).

Delivered (2026-11-05). ADR-0086 decision 4 already decides the behaviour; the choices below are
implementation defaults within it, so no new ADR.

- `QuicDatagramFrame(Data, HasLength = true)`: type 0x30 reads to the end of the packet, 0x31 its Length.
  `QuicFrameReader` takes both in 0-RTT and 1-RTT only (RFC 9221 section 4); 0x1f to 0x2f and 0x32 up stay
  `FRAME_ENCODING_ERROR`. The reader accepts the frame whatever was negotiated; the connection judges it.
- `QuicTransportParameters.MaxDatagramFrameSize` (0x20), written last, after `version_information`.
- `QuicServerConnectionOptions.MaxDatagramFrameSize`, null by default, so ADR-0082 decision 4's bytes stand
  (the existing byte-for-byte test still passes unedited). A set 0 is sent as 0 and refuses every frame.
- Received frames come back on `QuicConnectionOutput.ReceivedDatagramFrames`, an init property defaulting to
  empty, so no existing constructor call changed. Size is judged on the whole frame, type and length included
  (section 3); over it or without the parameter is `PROTOCOL_VIOLATION` carrying the frame's own type.
- `QuicServerConnection.SendDatagram(data)` always sends type 0x31: a 0x30 frame must end its packet, and
  the packet builder appends frames in order, so the Length keeps any frame after it valid. It queues with the
  other 1-RTT frames and waits for the congestion window rather than being dropped (section 5 allows either).
  It refuses before the handshake, when the client's parameter is absent or 0, and over the client's limit.
- Loss: `Retransmittable` drops `DATAGRAM` frames; the packet is still ack-eliciting and in flight, so loss
  detection and NewReno count it (test `SendDatagram_DatagramLost_IsNotSentAgainButCountsAsACongestionEvent`
  reads the window through a new internal `QuicServerConnection.Congestion`).
- Follow-up filed: BL-753, the same through `QuicApplicationConnection` and the flow loop, which HTTP/3 rides;
  BL-411 now depends on it too.
- Coverage of `Surl.Quic.UnitLibrary` after the change: 100% line, 100% branch (420 tests).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Quic sends max_datagram_frame_size when its options set it, receives DATAGRAM frames 0x30/0x31 into QuicConnectionOutput and sends them with SendDatagram, never retransmitted, PROTOCOL_VIOLATION when not offered or oversized
