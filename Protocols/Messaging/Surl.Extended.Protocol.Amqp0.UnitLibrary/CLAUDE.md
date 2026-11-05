# Surl.Extended.Protocol.Amqp0.UnitLibrary

An extended server (ADR-0083): it will answer the `amqp` and `amqps` schemes the Curl port adds,
which upstream curl does not have. ADR-0112 decides how. It is judged by its specification,
AMQP 0-9-1 (2008-11-13) with its XML protocol definition and RabbitMQ's errata, never by the
Curl port's output, and its unit tests replay bytes written by hand from that specification.

What is here so far is the protocol header, the framing and the data-type codec (BL-504,
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

The connection class, logins, tuning and heartbeats are BL-505; channels, exchanges, queues and
the basic class BL-506; the command line and composition BL-507.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference the
horizontal libraries ADR-0112 decision 0 names (`Surl.MessageBus`, `Surl.Content`) - never
another protocol server, original or extended. Never construct a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the listener seam.
