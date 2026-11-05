# ADR-0096 — How a Surl.Quic connection carries streams and flow control

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-378, which built it in `Surl.Quic.UnitLibrary`.
- **Builds on:** [ADR-0082](ADR-0082-how-surl-serves-http-3-over-quic.md) decision 4 (the
  initial credit the server grants, and "the server widens the windows ... as it consumes data and
  streams close") and decision 7 (`STREAM_LIMIT_ERROR` and `FLOW_CONTROL_ERROR` as the answers to
  a client past its credit).

## Context

BL-377 left `QuicServerConnection` handing every 1-RTT stream and flow-control frame to its
caller as `QuicConnectionOutput.ApplicationFrames`. HTTP/3 (BL-383) needs streams, not frames: data
put back in order, credit granted as it is read, and the client held to the credit. RFC 9000
sections 2 to 4 and 19.4 to 19.14 say what the frames mean and which faults end the connection;
they leave the server's API, when it grants credit, and some SHOULDs to the implementation.

What upstream curl sends that matters here was measured for ADR-0082 (its Measured table) and is
replayed by the connection tests from the pinned recordings: every build grants
`initial_max_stream_data_bidi_local` 32768, `initial_max_stream_data_uni` and `initial_max_data`
1048576000, and 262144 streams of each kind. So a response on curl's request stream is carried 32
KiB at a time, through curl's `MAX_STREAM_DATA`.

## Decision

1. **Streams are handles; the output names the ones reached.** A `QuicStream` is one stream: `Read`
   copies the bytes received in order, `Write(data, fin)` queues bytes to send, `Reset` sends
   `RESET_STREAM`, `StopSending` sends `STOP_SENDING`, and `ResetErrorCode`,
   `StopSendingErrorCode`, `IsReadComplete`, `ReadableLength` and `UnsentLength` say where it
   stands. `QuicConnectionOutput.Streams` lists the streams the client's frames reached in a
   `ReceiveDatagram` call, in the order first reached; it replaces `ApplicationFrames`, since every
   1-RTT frame RFC 9000 defines is now the connection's own. Nothing a stream queues is sent until
   `Transmit()` or the next `ReceiveDatagram`, so a caller batches its writes into datagrams.
2. **The client's streams open as their frames arrive.** A frame naming a client stream opens it
   and every lower one of its kind (RFC 9000 section 3.2), up to the `MAX_STREAMS` granted; past it
   is `STREAM_LIMIT_ERROR`. The server opens only unidirectional streams (3, 7, 11 ...; HTTP/3's
   control and QPACK streams), through `OpenUnidirectionalStream`, which returns null and sends
   `STREAMS_BLOCKED` when curl's `MAX_STREAMS` allows no more. A `MAX_STREAMS` for bidirectional
   streams is ignored, since the server opens none.
3. **Stream state errors.** `STREAM_STATE_ERROR` for a `STREAM`, `RESET_STREAM` or
   `STREAM_DATA_BLOCKED` naming a server stream (it has no receiving part), a `STOP_SENDING` or
   `MAX_STREAM_DATA` naming a client unidirectional stream (no sending part), and either naming a
   server stream not yet opened - every server bidirectional stream among them.
4. **Credit is granted a window at a time, at half.** When a read leaves less than half the initial
   window of a stream's credit unused, the stream's limit becomes what has been read plus the
   initial window (`MAX_STREAM_DATA`); the connection does the same with `initial_max_data`
   (`MAX_DATA`), counting bytes read and the unread bytes a `RESET_STREAM` discards. A stream whose
   `FIN` has arrived is granted nothing more. Granting at half keeps one frame per half window,
   and a client sending as fast as it may never waits a round trip on the server.
5. **A stream closes when both parts are done, and is then forgotten.** The receiving part is done
   when every byte up to `FIN` is read or the client reset it; the sending part when its `FIN` or
   `RESET_STREAM` is in a frame (there is no retransmission until BL-379, so "in a frame" is
   "sent"). Each client stream that closes raises `MAX_STREAMS` for its kind to the streams closed
   plus the initial count, sent once per `Transmit`. A later frame for a closed stream is ignored.
6. **Final size.** Data past a known final size, a second final size, or a final size below the
   data already received is `FINAL_SIZE_ERROR` (RFC 9000 section 4.5), from `STREAM` or
   `RESET_STREAM` alike; data or a final size past the stream's credit, or bytes past the
   connection's, is `FLOW_CONTROL_ERROR`. Each carries the frame type that broke it.
7. **Resets.** A client's `STOP_SENDING` is answered with `RESET_STREAM` carrying its code (the
   RFC 9000 section 3.5 SHOULD), its final size the bytes put in frames; the unsent rest is
   discarded. A reset of a sending part whose `FIN` is already sent, or that is already reset, sends
   nothing. A client's `RESET_STREAM` discards the unread data and frees its connection credit.
8. **Blocked frames.** The server sends `STREAM_DATA_BLOCKED` or `DATA_BLOCKED` once for each limit
   of curl's that holds data back (RFC 9000 section 4.1's SHOULD), and `STREAMS_BLOCKED` once per
   stream limit. The client's own blocked frames need no answer: the server grants credit as data
   is read, not on request.
9. **Frames.** A `STREAM` frame carries at most `MaxApplicationFrameLength` less 19 bytes of data
   (the longest type, stream ID, offset and length), so it fits in a 1-RTT packet alone. Until
   congestion control (BL-379) everything the credit allows is framed at once.

## Alternatives considered

- *Keep handing frames to the caller.* It would make HTTP/3 implement RFC 9000's stream rules
  itself, and every other user of `Surl.Quic` (DoQ, ADR-0092) again.
- *Grant credit on every read.* One `MAX_STREAM_DATA` per read is many frames for nothing; RFC 9000
  section 4.2 warns against it.
- *Grant credit only when the client says it is blocked.* It costs the client a round trip each
  window, and RFC 9000 does not require a client to send blocked frames.
- *Throw from `OpenUnidirectionalStream` past the limit.* The limit is the client's to raise, so it
  is a state to wait out, not a fault.

## Consequences

- BL-383 reads and writes HTTP/3 through `QuicStream` and calls `Transmit` after its writes.
- BL-379's retransmission will need a stream's sent data kept until acknowledged; decision 5's
  "sent" then becomes "acknowledged", as RFC 9000 section 3.1's Data Recvd state says.
