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

Produce (BL-519, ADR-0105 decisions 3, 4, 6 and 7, and its BL-519 amendment):

- `KafkaRecordBatchReader` reads and checks one partition's record batches - header length,
  `magic` 2, the CRC-32C (`KafkaCrc32C`, `BitOperations.Crc32C`), the attributes, the record count,
  offset deltas and lengths - into `KafkaProducedRecord`s, or the first failing batch's error code.
  Compressed batches are decoded first (below).
- `KafkaProduceAnswerer` answers v3 to v8: `acks` 0, 1 and -1 (0 writes no response), the topic
  checks (17, 3, 29), the batch checks, the record size limit (10) and the bus bounds (44), then
  publishes each record on `Surl.MessageBus` (value as payload, key, UTF-8 headers, QoS 1), so live
  subscribers of the mapped topic receive it, and answers the first record's log offset.

Fetch and ListOffsets (BL-520, ADR-0105 decisions 1, 3, 4 and 6):

- `KafkaFetchableTopic` makes the topic checks both share, in order: invalid name 17, partition
  other than 0 3, no subscribe access 29 (whether or not the topic exists), no log 3.
- `KafkaRecordBatchWriter` builds served batches from bus log records: one starting at the
  offset asked, LogAppendTime (attribute bit 3), no codec, at most 1000 records, within a byte
  limit, the response's first record written whatever it costs (KIP-74).
- `KafkaFetchAnswerer` answers v4 to v11: `OFFSET_OUT_OF_RANGE` (1) outside the log, high
  watermark and last stable offset the log end, `max_bytes` capped at `--max-message`, a failed
  partition -1 in every offset and empty records. Short of `min_bytes` it waits up to
  `max_wait_ms` (at most 60000) on `ExchangeContext.TimeProvider`, woken by a bus subscription to
  the request's topics, and reads once more when the wait ends. A `session_id` above 0 is
  top-level 70.
- `KafkaListOffsetsAnswerer` answers v1 to v5: -2 (or below) the log start, -1 the log end, any
  other timestamp the first record published at or after it; a failed partition -1 throughout.

Compressed batches (BL-521, ADR-0105 decision 4):

- `KafkaCompressionCodec` is the attributes' codec, 0 to 4. `KafkaRecordsCompression` decodes a
  produced batch's records - gzip with `GZipStream`, Snappy (raw or xerial), LZ4 and zstd with
  `Surl.Compression` - within `DecompressionBound` (`--max-message` times 4, 256 MiB when it is 0):
  past it `MESSAGE_TOO_LARGE` (10), an undecodable payload or a codec 5 to 7 `CORRUPT_MESSAGE` (2).
  Each decoded record becomes one bus message, as an uncompressed one does.
- `KafkaFetchAnswerer` takes the served codec as a constructor parameter (default none);
  `KafkaRecordBatchWriter` compresses each batch's records with it and names it in the attributes.
  Records are taken while their uncompressed bytes fit the limit; a compressed batch that still
  passes it gives up its last records until it fits.

Each served API gets an `IKafkaApiAnswerer`, passed to `KafkaBroker`'s constructor; the command
line (`--kafka-fetch-compression`) and composition are BL-522.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference the
horizontal libraries ADR-0105 decision 0 names (`Surl.MessageBus`, `Surl.Compression`) - never
another protocol server, original or extended. Never construct a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the listener seam.
