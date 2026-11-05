# Surl.Extended.Protocol.Kafka.UnitLibrary

An extended server (ADR-0083): it answers the `kafka` and `kafkas` schemes the Curl port adds,
which upstream curl does not have. ADR-0105 decides how. It is judged by its specification, the
Apache Kafka protocol guide for Kafka 4.4, never by the Curl port's output, and its unit tests
replay bytes written by hand from that guide.

What is here so far is the framing, the headers and the primitive-type codec (BL-517, ADR-0105
decisions 1 and 7):

- `KafkaPrimitiveReader` and `KafkaPrimitiveWriter` read and write every primitive type of the
  guide's "Protocol Primitive Types": BOOLEAN, INT8 to INT64, UINT32, VARINT, VARLONG,
  UNSIGNED_VARINT, UUID, FLOAT64, STRING, NULLABLE_STRING, BYTES, NULLABLE_BYTES (RECORDS), ARRAY
  lengths, and the COMPACT forms with tagged-field sections. A read that cannot complete throws
  `KafkaMalformedRequestException`.
- `KafkaRequestFrameReader` reads size-prefixed requests however they are split; a size of zero
  or less, or over `--max-message`, is refused from the prefix alone.
- `KafkaRequestHeader` reads request headers v0 to v2; `KafkaResponseFrame` writes the size prefix
  and response header v0 or v1, echoing the correlation id.
- `KafkaServedApis` is ADR-0105 decision 1's table of served APIs and versions;
  `KafkaApiVersionRange` chooses each version's request and response header versions.
- `KafkaBroker` is the `IConnectionProtocolServer` (`kafka`, `kafkas`); its doc comment lists every
  answer. `KafkaBrokerConnection` answers one connection, one request at a time, in order. An
  ApiVersions request above version 3 is answered with `KafkaApiVersionsFallback` (version 0,
  `UNSUPPORTED_VERSION`, every range); any other API or version outside the table, and a served
  API no `IKafkaApiAnswerer` answers yet, closes the connection with no bytes and a log note.

ApiVersions, Metadata and SASL (BL-518, ADR-0105 decisions 1, 2 and 5):

- `KafkaBroker` takes an `IAuthenticationPolicy`, asked once a connection whether an anonymous
  caller is let in (`--allow-anonymous`). When not, only ApiVersions, SaslHandshake and
  SaslAuthenticate are served before a login; anything else closes with no bytes, and
  `--head-timeout` runs from accept until the login is accepted.
- `KafkaConnectionState` is what the broker keeps of one connection (its TLS session, the
  pending `KafkaSaslLogin`, the account logged in); every `KafkaRequest` carries it.
- `KafkaApiVersionsAnswerer` answers v0 to v3 with `KafkaServedApis`' ranges.
- `KafkaMetadataAnswerer` answers v0 to v8 from `Surl.MessageBus`: node 0 as broker and
  controller, a random 22-character cluster id, one partition a topic, auto-creation through
  `MessageBus.CreateLog`, topic access through `MessageBus.GetTopicAccess`.
- `KafkaSaslHandshakeAnswerer` offers the policy's `PLAIN`, `SCRAM-SHA-256` and `SCRAM-SHA-512`,
  matched exactly; `KafkaSaslAuthenticateAnswerer` carries each step in `auth_bytes`, and a v0
  handshake's raw tokens are answered by `KafkaBrokerConnection`. `KafkaSaslLogin` hands each
  client message to the policy's `ISaslExchange`, and returns SCRAM's server-final with the
  success, since a Kafka client sends nothing after it.

Each served API gets an `IKafkaApiAnswerer`, passed to `KafkaBroker`'s constructor: Produce
(BL-519), Fetch and ListOffsets (BL-520), compressed batches (BL-521) are still to come; the
command line and composition are BL-522.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference the
horizontal libraries ADR-0105 decision 0 names (`Surl.MessageBus`, `Surl.Compression`) - never
another protocol server, original or extended. Never construct a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the listener seam.
