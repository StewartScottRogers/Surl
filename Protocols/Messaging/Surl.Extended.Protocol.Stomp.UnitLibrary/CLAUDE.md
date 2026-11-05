# Surl.Extended.Protocol.Stomp.UnitLibrary

An extended server (ADR-0083) for the `stomp` scheme the Curl port adds, which upstream curl
does not have; the composition registers it in BL-533, so `surl` does not serve it yet.
ADR-0118 decides how. It is judged by the STOMP 1.2 specification
(<https://stomp.github.io/stomp-specification-1.2.html>, read 2026-11-05), never by the Curl
port's output, and its unit tests replay frames written by hand from that specification and its
examples. `Documentation/Conformance/Extended/Stomp.md` traces each requirement to its task.

It references `Surl.Protocol.Abstractions` and `Surl.MessageBus`, the bus every messaging
server of one `surl` shares; `StompServer` takes the bus in its constructor. `stomps` comes
with BL-533.

Frame codec and connection negotiation (BL-531, ADR-0118 decisions 0 to 3, 6 and 8):

- `StompServer` is the `IConnectionProtocolServer` for `stomp` and its refusal writer (ERROR
  `Too many connections`); its remarks list every answer.
- `StompServerConnection` answers one connection: CONNECT or STOMP within the head timeout
  (closed with no bytes without one), the version, `heart-beat` and login checked in that order,
  CONNECTED (`version`, `heart-beat` but in 1.0, a random 32-hex-digit `session`, `server:surl`),
  then frames until DISCONNECT (RECEIPT first when it carries `receipt`), the client's close, a
  silent heart-beating client, a slow consumer or an ERROR. Every ERROR is written escaped by
  the negotiated version, writes are completed, and the server lingers up to `Linger` (1 s) on
  the `TimeProvider`, discarding what the client still sends.
- `StompFrameReader` reads frames: LF or CR LF, EOLs between frames skipped, any split across
  reads; headers decoded by `Version` (never on CONNECT or STOMP; nothing before a version), the
  first of a repeated name kept; `content-length` bodies with NULs; limits `--max-line` (EOL
  included), `MaxHeaders` (128) and `--max-message`. A part frame at end of stream is dropped.
- `StompFrameWriter` writes frames with LF line ends and a NUL, escaping by a version or not at all.
- `StompHeaderEscaping` is "Value Encoding" by version; `StompVersionNegotiation` chooses the
  version; `StompHeartBeat` reads and writes the `heart-beat` header; `StompErrors` holds the
  ADR's fixed `message` texts and the version ERROR.
- `StompServerOptions.HeartBeatWanted` is a quarter of the idle timeout, 30 s when it is off.

Routing through the bus (BL-532, ADR-0118 decisions 4 to 8):

- `StompServerConnection.Routing.cs` serves SEND, SUBSCRIBE, UNSUBSCRIBE, ACK, NACK (not in
  1.0), BEGIN, COMMIT, ABORT and DISCONNECT, and answers a `receipt` with RECEIPT once the frame
  took effect. `/topic/<t>` maps through `MessageBus.Names` for the scheme (aliases included) to
  a live bus subscription; `/queue/<q>` is the bus queue, declared durable on first SEND or
  SUBSCRIBE after the `$queue/<q>` access check, with competing consumers started after the
  SUBSCRIBE's RECEIPT. Every bus refusal is ERROR then close: `Access refused`,
  `Too many subscriptions`, `Frame too large` (the bus's message bound), otherwise
  `Resource limit exceeded`.
- `StompAckMode`: `auto` acknowledges a queue delivery as its MESSAGE is queued; `client`
  settles cumulatively per subscription and `client-individual` singly, each outstanding message
  a `StompOutstandingMessage`. ACK and NACK name `id` in 1.2 and `message-id` before; an unknown
  one is ignored. NACK requeues a queue message and drops a topic one. Ending a subscription
  (UNSUBSCRIBE or the connection's end) disposes its bus consumer, which puts its unacknowledged
  messages back.
- `StompTransaction` holds a transaction's SEND, ACK and NACK frames for COMMIT: at most
  `MaxOpenTransactions` (100) open and `--max-filesize` held body bytes; open ones are aborted
  when the connection ends.
- `StompMessageFrames` turns a SEND into a bus message (QoS 1, `content-type`, other headers in
  order) and a bus message into MESSAGE (`destination`, `message-id`, `subscription`, `ack` in
  1.2 client modes, `content-type`, `content-length`, then the bus headers but STOMP's own, any
  holding U+0000 and, in 1.0, any it cannot write unescaped).
- Once connected, every byte goes through one `StompOutbox`, bounded at `PendingBytesLimit`
  (64 MiB): past it the connection is cut off with a best-effort ERROR `Resource limit exceeded`.
- `StompServerConnection.HeartBeats.cs` sends an EOL every `max(1000, cy)` ms in which nothing
  else was queued, and closes with no bytes when nothing arrived for twice `max(cx, sy)` ms,
  timed from `StompFrameReader.LastReceivedAt`.
