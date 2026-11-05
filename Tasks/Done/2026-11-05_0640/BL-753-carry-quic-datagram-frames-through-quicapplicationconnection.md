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
completed: 2026-11-05
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

- [x] Named tests: `QuicApplicationConnection` sends a datagram the test client receives in a `DATAGRAM` frame
      (type 0x31); a `DATAGRAM` frame the test client sends is returned by an awaited receive call; once the
      connection ends a waiting receive ends as `ReadAsync` does and a send throws `OperationCanceledException`;
      sending when the client offered no `max_datagram_frame_size` throws `InvalidOperationException`.
- [x] Every existing `Surl.Quic.UnitTests` expected byte is unedited.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-573, whose acceptance named `QuicServerConnection` only.

Delivered (2026-11-05, lane 7):
- `QuicApplicationConnection.SendDatagramAsync(data, token)` calls `QuicServerConnection.SendDatagram`
  under the connection's lock and sends its output; once the connection has ended it throws
  `OperationCanceledException` like the other calls, and `SendDatagram`'s own `InvalidOperationException`
  (client offered no `max_datagram_frame_size`) and `ArgumentOutOfRangeException` pass through.
- `ReceiveDatagramAsync(token)` returns `byte[]?`: a copy of each received frame's data (the frame's
  `Data` is a slice of the decrypted packet, so a copy keeps it valid after the call), in order;
  null once the connection has ended and nothing is left, as `AcceptStreamAsync` and `ReadAsync` behave.
  `ApplyAsync`, which the flow loop calls with every `ReceiveDatagram`, queues the frames.
- Decision (default taken): the queue is bounded at `ReceivedDatagramCapacity` = 128 frames and drops
  the oldest past it. An unbounded queue would let a client grow server memory without limit while the
  application does not read; RFC 9221 section 5 lets a receiver drop any `DATAGRAM` frame, and dropping
  the oldest keeps the freshest data, which is what unreliable real-time datagrams want. No upstream
  curl bytes are involved (this is in-process plumbing; the wire bytes are BL-573's).
- Tests: `QuicApplicationConnectionTests.Datagrams.cs` -
  `SendDatagramAsync_ClientOffersDatagramFrames_ReachesTheClientInADatagramFrameWithLength`,
  `ReceiveDatagramAsync_ClientSendsDatagramFrames_ReturnsEachFramesDataInOrder`,
  `ReceiveDatagramAsync_ConnectionEndsWhileWaiting_ReturnsNullAndSendDatagramAsyncThrowsOperationCanceled`,
  `SendDatagramAsync_ClientOfferedNoMaxDatagramFrameSize_ThrowsInvalidOperation`. The only edit to an
  existing test file: `ClientParametersOfferingDatagramFramesOf1024Bytes` went from private to internal
  (its bytes unchanged), and `QuicApplicationConnectionTests` became partial.
- Coverage of `Surl.Quic.UnitLibrary`: line 1.0, branch 1.0 (cobertura, fast tests). Not tested: the
  drop-oldest overflow itself, which is the BCL channel's behaviour, not a branch of ours.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. QuicApplicationConnection sends DATAGRAM frames (SendDatagramAsync) and awaits received ones (ReceiveDatagramAsync) while the flow loop drives the connection
