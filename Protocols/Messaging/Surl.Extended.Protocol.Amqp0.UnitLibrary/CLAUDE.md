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
Channels, exchanges, queues and the basic class (BL-506, ADR-0112 decisions 5, 6 and 7):

- `Amqp0Broker` takes the process's one `MessageBus` and holds one `Amqp0ExchangeTable`, shared
  by its connections: `amq.direct`, `amq.fanout`, `amq.topic`, `amq.headers` and `amq.match` from
  start, and the direct, fanout, topic and headers exchanges clients declare (`Amqp0TopicKeyMatcher` matches `*` and `#`), with
  their bindings. The default exchange routes by queue name; `amq.topic` is the bus's topic space,
  its publishes `BusConnection.Publish` and its bindings `BindQueue`, mapped through
  `AmqpTopicExchangeNames`. Queues are the bus's named queues; nothing of a queue is held here.
- After `open-ok` each connection opens one `BusConnection` for its account, and every frame but a
  heartbeat goes through one ordered outbound queue, so a delivery the bus makes on another thread
  never overtakes the answer before it. Deliveries (`Deliver`) run on the bus's thread and only
  queue bytes, under the channel's `Gate`.
- `Amqp0BrokerConnection.Channels` dispatches each method on an open channel through
  `MethodAnswers`. A handler throws `Amqp0ChannelException` (answered `channel.close` naming the
  method; the channel's consumers end and its deliveries go back) or `Amqp0ConnectionException`
  (answered `connection.close`). Each bus refusal maps through `ChannelRefusals`; any it does not
  name is connection exception 506.
- `basic.publish` content is collected in `Amqp0PendingPublish` and stored with the native form
  `amqp0` (`Amqp0BusMessages.ToBusMessage`); a delivery of another protocol's message gets a header
  built from its fields (`ToOutgoingContent`). `Amqp0BasicProperties` reads and writes the 14
  `basic` properties.
- Delivery tags run from 1 per channel; `Amqp0Channel.Outstanding` maps each to its bus delivery.
  `basic.qos`'s prefetch-count bounds each consumer the channel makes afterwards; the
  channel-wide and `global` bounds, `prefetch-size`, and `recover` to the original consumer are
  BL-760. `basic.cancel` to the consumers of a deleted queue is BL-761.

Transactions and publisher confirms (BL-731, ADR-0112 decision 6):

- `tx.select` gives the channel an `Amqp0Transaction`, which buffers its whole publishes and its
  settlements (`Amqp0TransactedWork`) in order; `commit` routes and settles them, `rollback` or the
  channel's close puts the settled deliveries back in `Outstanding`. The buffered bodies stay in
  `heldContentBytes`, the connection's `--max-filesize` bound.
- `confirm.select` sets `Amqp0Channel.Confirming`: each whole publish is numbered from 1 and
  answered `basic.ack` (`multiple` clear), or `basic.nack` when routing threw 506.
- `tx` and `confirm` exclude each other with 406; `publisher_confirms` is in `connection.start`.

The headers exchange and durable exchanges across restarts (BL-732, ADR-0112 decisions 5 and 6):

- `amq.headers` and `amq.match` are pre-declared, and `exchange.declare` takes `headers`.
  `Amqp0HeadersMatcher` matches a binding's arguments against the message's `headers` (`x-match`
  `all` by default or `any`; `x-` entries not matched; a void entry matches presence); a binding
  with another `x-match` is 406. Each `Amqp0Binding` carries its arguments, kept and told apart
  only on a headers exchange.
- `Amqp0Broker.LoadExchangesAsync` reads `Amqp0ExchangeFiles` (`.surl/amqp0/exchanges`, through
  `IContentFileSystem`) once before serving; a malformed file is `Amqp0ExchangeLoadException`, which
  the composition answers 37. `Amqp0ExchangeTable` counts changes to durable declared exchanges and
  their bindings, and the connection saves them after each channel frame, noting a failed write.

An open connection the engine cancels (limit or shutdown) is told `connection.close` 320 on a one-second deadline of its own (BL-749).

The command line and composition are BL-507, which also hands the broker its `Amqp0ExchangeFiles`.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference the
horizontal libraries ADR-0112 decision 0 names (`Surl.MessageBus`, `Surl.Content`) - never
another protocol server, original or extended. Never construct a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the listener seam.
