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

Each served API gets an `IKafkaApiAnswerer`, passed to `KafkaBroker`'s constructor: ApiVersions,
Metadata and SASL (BL-518), Produce (BL-519), Fetch and ListOffsets (BL-520), compressed batches
(BL-521); the command line and composition are BL-522.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference the
horizontal libraries ADR-0105 decision 0 names (`Surl.MessageBus`, `Surl.Compression`) - never
another protocol server, original or extended. Never construct a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the listener seam.
