# Surl.Extended.Protocol.Stomp.UnitLibrary

An extended server (ADR-0083) for the `stomp` scheme the Curl port adds, which upstream curl
does not have; the composition registers it in BL-533, so `surl` does not serve it yet.
ADR-0118 decides how. It is judged by the STOMP 1.2 specification
(<https://stomp.github.io/stomp-specification-1.2.html>, read 2026-11-05), never by the Curl
port's output, and its unit tests replay frames written by hand from that specification and its
examples. `Documentation/Conformance/Extended/Stomp.md` traces each requirement to its task.

It references `Surl.Protocol.Abstractions` only. Routing through `Surl.MessageBus` -
SEND, SUBSCRIBE, UNSUBSCRIBE, ACK, NACK, BEGIN, COMMIT, ABORT, receipts on them and heart-beats
on the wire - comes with BL-532, and `stomps` with BL-533.

Frame codec and connection negotiation (BL-531, ADR-0118 decisions 0 to 3, 6 and 8):

- `StompServer` is the `IConnectionProtocolServer` for `stomp` and its refusal writer (ERROR
  `Too many connections`); its remarks list every answer.
- `StompServerConnection` answers one connection: CONNECT or STOMP within the head timeout
  (closed with no bytes without one), the version, `heart-beat` and login checked in that order,
  CONNECTED (`version`, `heart-beat` but in 1.0, a random 32-hex-digit `session`, `server:surl`),
  then one more frame: DISCONNECT (RECEIPT first when it carries `receipt`) or an ERROR. Every
  ERROR is written escaped by the negotiated version, writes are completed, and the server
  lingers up to `Linger` (1 s) on the `TimeProvider`, discarding what the client still sends.
  Until BL-532, SEND and the other routing frames are answered ERROR `Unknown command`.
- `StompFrameReader` reads frames: LF or CR LF, EOLs between frames skipped, any split across
  reads; headers decoded by `Version` (never on CONNECT or STOMP; nothing before a version), the
  first of a repeated name kept; `content-length` bodies with NULs; limits `--max-line` (EOL
  included), `MaxHeaders` (128) and `--max-message`. A part frame at end of stream is dropped.
- `StompFrameWriter` writes frames with LF line ends and a NUL, escaping by a version or not at all.
- `StompHeaderEscaping` is "Value Encoding" by version; `StompVersionNegotiation` chooses the
  version; `StompHeartBeat` reads and writes the `heart-beat` header; `StompErrors` holds the
  ADR's fixed `message` texts and the version ERROR.
- `StompServerOptions.HeartBeatWanted` is a quarter of the idle timeout, 30 s when it is off.
