# Surl.Extended.Protocol.Amqp0.UnitLibrary

An extended server (ADR-0083) for the `amqp` and `amqps` schemes the Curl port adds, which
upstream curl does not have; the composition registers it in BL-507, so `surl` does not serve
them yet. ADR-0112 decides how. It is judged by its specification,
AMQP 0-9-1 (2008-11-13) with its XML protocol definition and RabbitMQ's errata, never by the
Curl port's output, and its unit tests replay bytes written by hand from that specification.

The protocol header, the framing and the data-type codec (BL-504,
ADR-0112 decisions 1 and 8's frame limits):

- `Amqp0ProtocolHeader` reads the client's first 8 bytes before writing anything: `AMQP 0 0 9 1`
  is accepted; any other 8 bytes are answered with `AMQP 0 0 9 1` and the write side completed
  (section 4.2.2); a client that closes sooner gets nothing.
- `Amqp0FrameReader` reads frames (section 4.2.3) however they are split. It refuses from the
  7-byte header alone an unknown frame type, a frame above the frame-max it is given (header and
  frame-end included), and a heartbeat off channel 0 (section 4.2.7); after the payload, a
  frame-end other than `0xCE`. Each refusal's `RefusalReplyCode` is 501 `FRAME_ERROR`.
- `Amqp0Frame` encodes a frame; `Amqp0MethodPayload` and `Amqp0ContentHeader` read and write the
  method and content-header payloads (the arguments and property list stay encoded). A content
  header with a second property-flags short is a syntax error: `basic` needs one.
- `Amqp0FrameLimits` holds `frame-min-size` (4096) and the largest frame-max offered (131072),
  and the frame-max to offer for a `--max-message`.
- `Amqp0WireReader` and `Amqp0WireWriter` read and write every data type of section 4.2.5:
  octet, short, long, long-long, packed bits, short and long strings, and field tables and arrays
  with every type octet RabbitMQ's errata list (`Amqp0FieldValueType`). Both `l` (RabbitMQ's) and
  `L` (the PDF's) read a signed 64-bit integer and keep their octet; the PDF's `U` and its `s`
  short string are unknown octets. Nesting deeper than 64 inside a table, an unknown type octet,
  malformed UTF-8 in a short string, or a field past the payload throws `Amqp0SyntaxException`
  (502 `SYNTAX_ERROR`).
- `Amqp0ReplyCode` is the XML's reply-code constants.

The connection class (BL-505, ADR-0112 decisions 2, 3, 4 and 7):

- `Amqp0Broker` is the `IConnectionProtocolServer` for `amqp` and `amqps`; its remarks list every
  answer. `Amqp0BrokerConnection` (partial: login, heartbeats, the open connection) answers one
  connection: header, `start`, the login, `tune`, `open` of the one virtual host `/`, then frames
  until a `close` either way.
- Logins go through `ISaslAuthenticationPolicy`: `AMQPLAIN` is turned into a `PLAIN` response
  (`Amqp0ConnectionStartOk.ToPlainResponse`), challenges travel as `secure`/`secure-ok`, and a
  refusal is `connection.close` 403. `ANONYMOUS` is offered only with
  `Amqp0BrokerOptions.OffersAnonymous`.
- Heartbeats run on `ExchangeContext.TimeProvider`: a heartbeat whenever nothing was written for
  the interval, and a peer silent for two intervals is closed without `connection.close`.
- `Amqp0ConnectionStart`, `Amqp0ConnectionStartOk`, `Amqp0ConnectionTuning`, `Amqp0ConnectionOpen`
  and `Amqp0ConnectionClose` are the connection methods' codecs; `Amqp0ConnectionMethodId` their IDs.
- No channel opens yet: a frame on any channel but 0 is `connection.close` 504 until BL-506.

The 320 `CONNECTION_FORCED` close on the idle timeout, `--max-time` and shutdown is BL-743;
channels, exchanges, queues and the basic class BL-506; the command line and composition BL-507.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference the
horizontal libraries ADR-0112 decision 0 names (`Surl.MessageBus`, `Surl.Content`) - never
another protocol server, original or extended. Never construct a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the listener seam.
