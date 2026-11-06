# ADR-0105 — How the Kafka broker answers `kafka` and `kafkas`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-516. BL-517 builds the framing, headers and primitive-type codec and the answer to an API or
  version outside the ranges (decisions 1 and 7), BL-518 ApiVersions, Metadata and SASL (decisions 1,
  2 and 5), BL-519 Produce (decisions 3, 4, 6 and 7), BL-520 Fetch and ListOffsets (decisions 1, 3
  and 6), BL-521 compressed batches on produce and fetch (decision 4), and BL-522 the command line and
  the composition (decisions 4 and 7). The codecs BL-521 needs are built in a new
  `Foundation/Surl.Compression.UnitLibrary` by BL-702 (Snappy, xerial framing, the LZ4 frame, XXH32),
  BL-698 (zstd decoding) and BL-699 (zstd encoding); BL-700 adds SCRAM-SHA-512 to
  `Surl.Authentication` (decision 5); BL-701 decides consumer groups, committed offsets, CreateTopics
  and InitProducerId (decision 1, "Not served yet").
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof),
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decision 4 (Kafka logs in through `ISaslAuthenticationPolicy`),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  (topics, the log and the accounts are the shared message bus's and the shared account set's) and
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decision 3 (declared topics and topic access).
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3's table: a new
  horizontal library, `Foundation/Surl.Compression.UnitLibrary` (decision 4), which any server may
  reference. ADR-0100 decision 3.4's row "Kafka batching and compression" stands and is completed by
  decision 4 below: how a compressed batch from a producer becomes bus messages, and how a consumer
  can be served compressed batches.

## Context

`Surl.Extended.Protocol.Kafka` (in `Protocols/Messaging/`, ADR-0083's table) answers the `kafka`
and `kafkas` schemes the Curl port adds, on ports 9092 and 9093 (ADR-0083 decision 7). By ADR-0083
decision 1 it is judged by its specification, not by upstream curl, which has no Kafka, and not by
the Curl port. The specification read, and pinned, is:

- **the Apache Kafka protocol guide for Apache Kafka 4.4** (<https://kafka.apache.org/44/design/protocol>,
  read 2026-11-05; below "the guide"), its sections "Preliminaries" (network, partitioning and
  bootstrapping, "Retrieving Supported API versions", "SASL Authentication Sequence"), "The Protocol"
  ("Protocol Primitive Types", "Notes on reading the request format grammars", "Common Request and
  Response Structure", "Request and Response Headers", "Record Batch", "Error Codes", "Api Keys") and
  "The Messages";
- the "Record Batch" and "Record" sections of the Kafka 4.4 documentation's "Message Format"
  (<https://kafka.apache.org/documentation/#recordbatch>): magic 2, the CRC-32C from `attributes` to
  the batch's end, the attribute bits (codec in bits 0 to 2, timestamp type bit 3, transactional bit
  4, control bit 5);
- RFC 4616 (SASL `PLAIN`), RFC 5802 and RFC 7677 (SCRAM);
- for the codecs of decision 4: RFC 1952 (gzip), google/snappy's `format_description.txt` and
  snappy-java's xerial stream framing, lz4/lz4's `doc/lz4_Frame_format.md` and
  `doc/lz4_Block_format.md`, and RFC 8878 (zstd).

The guide's version ranges, read from its "The Messages" section for 4.4 (Kafka 4.0 removed the
oldest versions, KIP-896):

| API (key) | Versions in the guide | First flexible version |
| --- | --- | --- |
| Produce (0) | 3 to 13 | 9 |
| Fetch (1) | 4 to 18 | 12 |
| ListOffsets (2) | 1 to 11 | 6 |
| Metadata (3) | 0 to 13 | 9 |
| SaslHandshake (17) | 0 to 1 | none |
| ApiVersions (18) | 0 to 5 | 3 |
| SaslAuthenticate (36) | 0 to 2 | 2 |

The port's `Curl.Extended.Protocol.Kafka.UnitLibrary` and its matrices `Kafka.md`, `Kafka-Records.md`
and `Kafka-Compression.md` were read only to learn what a client sends, so that the broker accepts it:
request header v1; **no ApiVersions**; SaslHandshake v1 (`PLAIN`) and SaslAuthenticate v0 when a login
is given; Metadata v4 naming one topic; then Produce v3 with `acks` -1 and one uncompressed batch, or
Fetch v4 from an offset with isolation level 0, consuming batches compressed with any of the four
codecs. Nothing below is taken from the port's output, and no expected byte comes from it.

## Decision

### 0. What the broker is

- **A single-node broker on the shared bus.** One `surl` is one Kafka cluster of one broker, node id
  **0**, which is also the controller. Its topics, their log and its accounts are not its own: they are
  ADR-0100's `Surl.MessageBus` and account set, handed to it by the composition (BL-522), so a record
  produced over `kafka` reaches an `mqtt` subscriber and a message published over `mqtt` is fetched
  over `kafka`. The library references `Surl.Protocol.Abstractions`, `Surl.MessageBus` and
  `Surl.Compression` (decision 4), never another protocol server.
- **State the broker keeps of its own** is per connection: the SASL state, the request in progress
  and, for `acks` 0, nothing more. Consumer groups and committed offsets, which ADR-0100 decision 3.3
  leaves to the broker, come with BL-701.
- **Requests on a connection are answered one at a time, in order** (the guide, "Network"). A Fetch that waits (decision 6) holds the
  requests behind it, as the guide's ordering requires.

  (The guide's sentence reads "The server guarantees that on a single TCP connection, requests will
  be processed in the order they are sent and responses will return in that order as well.")

### 1. The APIs and versions served (the guide, "Retrieving Supported API versions", "Request and Response Headers", "The Messages")

| API (key) | Served versions | Why this range |
| --- | --- | --- |
| Produce (0) | **3 to 8** | 3 is the guide's lowest; 8 the last non-flexible. 4 to 8 add only error codes and fields the broker fills (`log_start_offset` from v5, `record_errors` and `error_message` from v8, empty and null). |
| Fetch (1) | **4 to 11** | 4 is the guide's lowest; 11 the last non-flexible. v7's fetch sessions are declined (decision 6); v9's `current_leader_epoch` and v11's `rack_id` are read and ignored, the leader epoch being always 0 and `preferred_read_replica` always -1. |
| ListOffsets (2) | **1 to 5** | 1 is the guide's lowest; 5 the last non-flexible. |
| Metadata (3) | **0 to 8** | 0 is the guide's lowest; 8 the last non-flexible. |
| SaslHandshake (17) | **0 to 1** | the guide's whole range (decision 5). |
| ApiVersions (18) | **0 to 3** | 3 is flexible, and it is the version every current client sends first; serving it saves those clients the fallback round trip. 4 and 5 are answered by the fallback rule below. |
| SaslAuthenticate (36) | **0 to 1** | 1 is the last non-flexible; v1's `session_lifetime_ms` is 0 (no re-authentication). |

- **Headers.** A non-flexible request reads request header v1 (`api_key`, `api_version`,
  `correlation_id`, `client_id`) and is answered with response header v0 (`correlation_id`). The one
  flexible request served, ApiVersions v3, reads request header v2 (v1 plus tagged fields, which are
  read and ignored); its response keeps response header v0, as the guide says of ApiVersions. Request
  header v0 (no `client_id`) belongs to no served version. Every response echoes the request's
  `correlation_id`. `throttle_time_ms` is always 0: the broker throttles nobody.
- **ApiVersions** answers error code 0 and one row per API above, `min_version` and `max_version` as
  in the table, and nothing else; v3's `finalized_features` and `supported_features` are empty and its
  `zk_migration_ready` false. It may come at any time, before authentication included (decision 5).
  **An ApiVersions version above 3** is answered, as the guide's "Retrieving Supported API versions"
  says of a broker from 2.4.0 on, by **a version 0 ApiVersions response with error code
  `UNSUPPORTED_VERSION` (35)** and the full `api_versions` array, so the client can retry at the
  highest version both support.
- **Any other API key, or a served API at a version outside its range**, closes the connection with
  no bytes, and the exchange log notes it. The guide prescribes a reply to an unsupported version only
  for ApiVersions; a response to any other request is written in that request's version's schema,
  which does not exist for an unserved version, so no reply could be read. A client learns the ranges
  from ApiVersions, which the guide tells it to send first. `UNSUPPORTED_VERSION` is therefore sent
  only in the ApiVersions fallback.
- **Not served yet: consumer groups and committed offsets** (FindCoordinator, JoinGroup, SyncGroup,
  Heartbeat, LeaveGroup, OffsetCommit, OffsetFetch), **CreateTopics** (ADR-0100 decision 3.2's
  `INVALID_PARTITIONS` row) and **InitProducerId**. BL-701 decides them; until it lands they are
  outside the ranges and close the connection like any unserved API, and ApiVersions does not list
  them, so a client knows not to send them. A consumer that assigns its partition itself, as the
  port's client and every "assign" consumer does, needs none of them.

### 2. Topics and partitions (the guide, Metadata; ADR-0100 decision 3.2; ADR-0102 decision 3.1)

- **One partition, 0, a topic** (ADR-0100 decision 3.2). Its leader, only replica and only in-sync
  replica is node 0; `offline_replicas` (v5) is empty; `leader_epoch` (v7) is 0.
- **Names.** A Kafka topic name maps onto the bus with ADR-0100 decision 3.2's rule (`.` becomes
  `/`), through `Surl.MessageBus`'s `KafkaTopicNames`. A name outside `[A-Za-z0-9._-]{1,249}`, or
  `.` or `..`, is **`INVALID_TOPIC_EXCEPTION` (17)** wherever it appears: Metadata's topic row,
  Produce's and Fetch's partition rows, ListOffsets'. A bus topic Kafka cannot name (ADR-0100's "What
  does not survive a mapping") is never listed.
- **Created on first use, as Kafka's default `auto.create.topics.enable=true` does.** A topic exists
  for Kafka once the bus holds a log for it: once anything was published on it over any protocol, or
  it was declared with `--topic` (ADR-0102 decision 3.1). Metadata for a topic that does not exist
  creates its empty log when the request allows auto-creation (always for v0 to v3; v4 to v8 by
  `allow_auto_topic_creation`) and answers it with error 0 and partition 0; when it does not allow it,
  the row is **`UNKNOWN_TOPIC_OR_PARTITION` (3)** with no partitions. A Produce to a topic that does
  not exist creates it. A Fetch or ListOffsets of one does not create it and is answered `3`. Creating
  topics is the anonymous-safe default: a topic is only a name and an empty log, bounded by the bus
  (decision 7).
- **A partition other than 0** of any topic is **`UNKNOWN_TOPIC_OR_PARTITION` (3)** in Produce, Fetch
  and ListOffsets.
- **Metadata's other fields.** `brokers` holds node 0 alone, with the host and port the client
  reached: the listener's bound port, and its bound address, or the connection's local address when
  the listener is bound to a wildcard address (so a client reconnecting to the advertised address
  reaches the same `surl`; BL-522 passes it in). `rack` is null. `cluster_id` (v2) is a 22-character
  base64url string of 16 bytes from `RandomNumberGenerator`, fixed for the server's life, as a Kafka
  cluster id is. `controller_id` (v1) is 0. `is_internal` (v1) is false. A request naming no topics
  (null from v1, an empty array in v0) lists every topic the bus holds that Kafka can name, in ordinal
  order of the Kafka name. v8's `include_cluster_authorized_operations` and
  `include_topic_authorized_operations` are answered with `-2147483648` (not requested) when false;
  when true, with the bit set of the operations the caller has: DESCRIBE (8) always, READ (3) and
  WRITE (4) for a topic as ADR-0102 decision 3.4's access allows, and for the cluster DESCRIBE alone.
- **Topic access** (ADR-0102 decision 3.4): a topic a caller may neither publish nor subscribe to is
  answered in Metadata as **`TOPIC_AUTHORIZATION_FAILED` (29)** with no partitions, the same whether or
  not it exists, and is left out of an all-topics listing; Produce, Fetch and ListOffsets answer `29`
  as ADR-0102's Kafka row says.

### 3. The log (the guide, Fetch, ListOffsets; ADR-0100 decisions 3.3 and 3.7)

- **The log is the bus's topic log** (ADR-0100 decision 3.3, built by BL-633): one per topic, offsets
  from 0 and contiguous, one offset per bus message. It is held in memory, and under `--directory
  <path>` persisted in `<path>/.surl/bus` as ADR-0100 decision 3.7 says (BL-634); the broker never
  writes a file of its own.
- **Retention is the bus's**: by bytes, `DefaultMaxLogBytesPerTopic` (16 MiB) a topic, the oldest
  messages dropped first and the log start offset moving up; never by time. ADR-0100's expiry is not
  applied to the log.
- **The offsets a response reports**: `high_watermark` and `last_stable_offset` (v4) are both the log
  end offset (the next offset to be written): on one node every appended record is replicated and
  committed, and there are no transactions (decision 4). `log_start_offset` (Fetch v5, Produce v5) is
  the bus log's start offset. `aborted_transactions` is empty.
- **A record built from a bus message**: `offset` the log offset; `timestamp` the bus message's
  publish time in milliseconds since the Unix epoch, with the batch's timestamp type set to
  **LogAppendTime** (attributes bit 3), since that is the time the bus keeps and the guide defines
  `maxTimestamp` as the append time then; `key` the bus message's key, or null; `value` the payload,
  exact; `headers` the bus message's headers, in order, name and value UTF-8. A record's attributes
  are 0.
- **A bus message built from a produced record** (ADR-0100 decision 3.4): payload the value, a **null
  value stored as an empty payload** (the bus has no null payload; a fetched record then has an empty,
  not null, value, noted here as the one loss); key kept, null kept null; headers whose value is valid
  UTF-8 kept in order, others (and null values) dropped, which ADR-0100 decision 3.4 already says;
  quality of service 1; the producer's timestamp dropped, the bus's publish time taken instead, so the
  Produce response's `log_append_time_ms` (v2 on) is that time, never -1.
- **ListOffsets** (v1 to v5): timestamp **-2** is the log start offset, **-1** the log end offset, and
  any other timestamp the first offset whose publish time is at or after it, or offset -1 and timestamp
  -1 when none is; the answer's `timestamp` is that record's publish time, -1 for -2 and -1.
  `isolation_level` (v2) changes nothing (decision 4). `current_leader_epoch` (v4) is ignored. A
  timestamp below -2 is answered for -2 (v1 to v5 define no other negative value; read leniently).

### 4. Record batches and compression (the "Record Batch" section; the guide, Produce)

- **The checks, in order, for each batch in a partition's `records`** (BL-519): at least the 61 bytes
  of a batch header, and `batchLength` covering the rest of the batch within `records`; `magic` **2**,
  otherwise the partition is **`UNSUPPORTED_FOR_MESSAGE_FORMAT` (43)** (Produce v3 and later carry
  only magic 2; Kafka 4 removed the older formats); the **CRC-32C** (Castagnoli) of the bytes from
  `attributes` to the batch's end, computed with `System.Numerics.BitOperations.Crc32C`, equal to
  `crc`, otherwise **`CORRUPT_MESSAGE` (2)**; the records, after decompression, decoding to exactly
  `records count` records whose `offsetDelta`s are 0, 1, 2, ... and whose last is `lastOffsetDelta`,
  each record's `length` matching its bytes, otherwise `2`. A failing batch fails its whole partition
  in the request, and nothing of that partition is appended (a Produce is all or nothing per
  partition). `baseOffset`, `partitionLeaderEpoch` and `baseTimestamp` from the producer are ignored:
  the broker assigns offsets and the bus assigns times.
- **Transactions and control batches are not served.** A non-null `transactional_id`, or a batch with
  the transactional (bit 4) or control (bit 5) attribute, is **`UNSUPPORTED_FOR_MESSAGE_FORMAT` (43)**
  for its partition: there is no transaction coordinator (BL-701 decides InitProducerId). A batch with
  `producerId` 0 or above outside a transaction (an idempotent producer) is accepted; its sequence is
  not checked, as the broker never answers a retry differently from a first send on one node.
- **Compressed batches from a producer are decompressed and stored as messages.** The bus holds one
  message a record (ADR-0100 decision 3.4), so a record must exist on its own for an `mqtt` subscriber
  to receive it. The codec bits 1 to 4 are decoded with: gzip, the BCL's `GZipStream` (RFC 1952);
  Snappy, raw or in snappy-java's xerial framing (the magic `0x82 "SNAPPY" 0x00` chooses), the LZ4
  frame and zstd (RFC 8878, skippable frames skipped, a dictionary id refused), all hand-built in
  `Surl.Compression` (below). A codec value 5 to 7, or a payload its codec cannot decode, is
  **`CORRUPT_MESSAGE` (2)**. Decompression is bounded: the decompressed records of one batch may not
  pass `--max-message` (ADR-0006; 1 MiB by default) times 4, nor 268435456 bytes (256 MiB) when
  `--max-message` is 0, and a batch that would is **`MESSAGE_TOO_LARGE` (10)** without decoding the
  rest, so a small compressed request cannot expand without bound. The checksums inside LZ4 and zstd
  frames are checked when present; a mismatch is `2`.
- **What a Fetch serves**: batches the broker builds from the log (ADR-0100 decision 3.4's row; the
  producer's batch boundaries and codec are not kept), each with `baseOffset` the first record's
  offset, `partitionLeaderEpoch` 0, magic 2, attributes with LogAppendTime (bit 3) and the codec,
  `baseTimestamp` and `maxTimestamp` the first and last publish times, `producerId` -1,
  `producerEpoch` -1, `baseSequence` -1, records with offset deltas from 0, and the CRC-32C. A batch
  starts exactly at the requested `fetch_offset`, so a client never has records to skip. Records are
  added to a batch while it stays within the partition's `partition_max_bytes` and the request's
  `max_bytes`, except that the **first batch of the response is returned whole even when it is
  larger** (the guide, Fetch `max_bytes`: "See KIP-74 for cases where this limit may not be
  honored"; KIP-74 returns the first batch whole so that a consumer always makes progress).
  A batch holds at most 1000 records.
- **The codec of served batches is chosen by a new option, `--kafka-fetch-compression
  <none|gzip|snappy|lz4|zstd>`**, default `none`, so that a consumer's decompression can be exercised
  against `surl` without a producer that compresses. BL-522 adds it (category `kafka`, its
  `OptionArgumentType` and `--aihelp` facts); BL-521 builds the serving. The encoders:
  - gzip: the BCL's `GZipStream`, `CompressionLevel.Optimal`;
  - Snappy: a greedy hash-table matcher writing literals and copies with 1-, 2- and 4-byte offsets
    (google/snappy `format_description.txt`), in snappy-java's xerial framing, version 1 and
    compatible version 1, one chunk per 32 KiB of input, which is what Kafka's Java producer writes and
    what every Kafka consumer reads;
  - LZ4: one LZ4 frame (version 01, independent blocks, no block or content checksum, block maximum
    64 KiB, the header checksum byte from XXH32), its blocks from a greedy hash-table matcher, a block
    the matcher does not shrink stored uncompressed (high bit of the block size);
  - zstd: one frame with `Frame_Content_Size` and no checksum or dictionary, each block a
    `Compressed_Block` of raw literals and sequences in Predefined_Mode from a greedy matcher with
    repeat offsets, or a `Raw_Block` when that is not smaller, or an `RLE_Block` for a run of one byte.
- **`Surl.Compression`** (new, `Foundation/Surl.Compression.UnitLibrary` and its `.UnitTests`,
  namespace `Surl.Compression`, referencing nothing; amends ADR-0002 decision 3's table) holds the
  hand-built codecs and checksums: `SnappyDecoder`, `SnappyEncoder`, `XerialSnappyFraming`,
  `Lz4FrameDecoder`, `Lz4FrameEncoder`, `XxHash32` (BL-702); `ZstdFrameDecoder` with its Huffman and
  FSE decoders and `XxHash64` (BL-698); `ZstdFrameEncoder` (BL-699). It is a library rather than part
  of the Kafka server because the codecs are protocol-free and HTTP's `Content-Encoding: zstd` (which
  curl's `--compressed` accepts) can use the same code later; `Foundation/` is the category for a
  service every server may use (ADR-0079). It follows root `CLAUDE.md`'s rule for hand-built pieces:
  its own library, its own tests, the same quality gates, no package. Each decoder takes an output
  bound and stops with a typed outcome past it.

### 5. SASL (the guide, "SASL Authentication Sequence", SaslHandshake, SaslAuthenticate; RFC 4616; RFC 5802; RFC 7677)

- **Secure by default (ADR-0032).** Unless `--allow-anonymous` is given, a connection must
  authenticate before any request but **ApiVersions and SaslHandshake**. Another request first closes
  the connection with no bytes: the guide's sequence has no error answer for it, and a broker that
  replied would tell an unauthenticated peer about its topics (ADR-0006 section 3). The exchange log
  notes the close.
- **Mechanisms offered**: those `ISaslAuthenticationPolicy.GetSaslMechanisms` returns for the
  connection's scheme and TLS session, kept to **`PLAIN`, `SCRAM-SHA-256` and `SCRAM-SHA-512`** in that
  order (the mechanisms a Kafka broker implements; `SCRAM-SHA-1` is not, and is not offered). The
  policy already withholds `PLAIN` on a connection without TLS unless `--allow-plaintext-auth` (FR-028)
  is given, so `kafka://` offers SCRAM alone by default and `kafkas://` all three. `SCRAM-SHA-512` is
  offered once BL-700 adds it to `Surl.Authentication`; until then the list holds the other two.
  `GSSAPI` and `OAUTHBEARER` are not offered.
- **SaslHandshake** (v0 and v1): a mechanism in the offered list is answered error 0 with the list;
  another is **`UNSUPPORTED_SASL_MECHANISM` (33)** with the list, and the connection stays open for a
  second SaslHandshake. After a v1 handshake the exchange travels in SaslAuthenticate requests; after
  a v0 handshake, as the guide says, in raw tokens each an INT32 size and the bytes, with the server's
  tokens framed the same way, and a v0 failure closes the connection with no bytes (v0 has no error
  field for it). A second handshake on an authenticated connection closes it with no bytes.
- **SaslAuthenticate** (v0 and v1): `auth_bytes` are the mechanism's client messages: for `PLAIN` the
  RFC 4616 `authzid NUL authcid NUL passwd` (an `authzid` other than empty or the `authcid` is refused);
  for SCRAM the RFC 5802 client-first and client-final messages, the server-first and server-final
  coming back in `auth_bytes`. A login the policy accepts ends the exchange with error 0; a refusal is
  **`SASL_AUTHENTICATION_FAILED` (58)** with the fixed `error_message` `Authentication failed`,
  naming neither account nor cause (ADR-0032 section 5), after the policy's one-second delay (ADR-0032
  section 8), and the connection is closed after the response is written, as a Kafka broker does.
  SaslAuthenticate without a handshake first closes the connection with no bytes. v1's
  `session_lifetime_ms` is 0 (no re-authentication, KIP-368's "no expiry").
- **Under `--allow-anonymous`** every request is served without a login, and a client that still
  authenticates runs its mechanism's steps and is accepted unchecked, as ADR-0049 decides for the mail
  servers' SASL. The authenticated account, or none, is the caller ADR-0102's topic access judges.
- **`--head-timeout`** (ADR-0006) bounds the time from accept to a finished login: past it the
  connection closes with no bytes.

### 6. `acks`, and how long a Fetch waits (the guide, Produce, Fetch)

- **`acks` 0**: the request is processed and **no response is written** (the guide, Produce `acks`:
  "0 for no acknowledgments"; a Kafka producer sending `acks` 0 reads no response). An error is then
  only noted in the exchange log.
- **`acks` 1 and -1** mean the same on one node: the response is written once every partition of the
  request has been appended to the bus (and the bus's persistence write attempted, ADR-0100 decision
  3.6's `StorageFailed` row: a failed write is noted, never answered as an error). `timeout_ms` is
  read and ignored: there is no replica to wait for.
- **Any other `acks`** is **`INVALID_REQUIRED_ACKS` (21)** for every partition of the request, nothing
  appended, and a response is written.
- **Fetch `max_wait_ms` and `min_bytes`**: when the records available at the request's offsets come
  to fewer than `min_bytes` bytes, the broker waits until they reach it, or until `max_wait_ms` has
  passed, whichever is first, then answers with what is there. The wait is on
  `ExchangeContext.TimeProvider` and wakes on the bus's append notification for the request's topics,
  never by polling or `Thread.Sleep`. `max_wait_ms` is capped at **60000** (half of ADR-0006's 120-s
  `--idle-timeout`, so a waiting client is never idled out; Kafka's own consumer default is 500).
  `min_bytes` 0 or 1 with records present, or any error in the response, answers at once.
- **Fetch sessions** (v7 to v11): the broker keeps none. Every response has `session_id` 0, which the
  guide defines as "0 if this is not part of a fetch session", and is a full response; a request with `session_epoch` -1 (a full
  fetch that wants no session) or 0 (asking for one) is served in full; a request continuing a session
  (`session_id` above 0) is answered with top-level error **`FETCH_SESSION_ID_NOT_FOUND` (70)** and no
  partitions, so the client starts again with a full fetch. `forgotten_topics_data` is ignored.
- **Fetch errors per partition**: a `fetch_offset` below the log start offset or above the log end
  offset is **`OFFSET_OUT_OF_RANGE` (1)** (equal to the log end offset is an empty, successful answer);
  an unknown topic or partition `3`; an invalid name `17`; no access `29`.

### 7. `kafkas`, and the ADR-0006 limits

- **`kafkas`** is the broker behind implicit TLS from the first byte, wrapped in
  `ImplicitTlsSchemeServer` by BL-522 as `mqtts` is (ADR-0010): TLS 1.2 and 1.3 (ADR-0006 section 4),
  `--cert` or `--self-signed` required (`CertificateProblem` (58) without), no client certificate
  asked for, no ALPN required (Kafka registers none; one offered is not agreed).

| ADR-0006 limit | How the broker applies it | Answer when hit |
| --- | --- | --- |
| `--max-message` (1 MiB) | a request's size prefix, Kafka's `socket.request.max.bytes`; a negative or zero size too | close with no bytes, the body never read (the guide gives no answer to a request that cannot be read, and a Kafka broker closes) |
| `--max-filesize` (100 MiB) | one record's value and key together, the bus's `MaxMessageBytes` (ADR-0100 decision 3.6), Kafka's `message.max.bytes` | the partition is `MESSAGE_TOO_LARGE` (10), nothing of it appended |
| the decompression bound (decision 4) | one compressed batch's records | `MESSAGE_TOO_LARGE` (10) |
| Fetch `max_bytes` | capped at `--max-message`, so no response passes the size the broker reads, except a first batch larger than that, returned whole (decision 4) | none: the response is smaller |
| the bus's store bounds (`StoreFull`, `TooManyTopics`) | ADR-0100 decision 3.6's Kafka column | `POLICY_VIOLATION` (44) for the partition, or, for Metadata's auto-creation, the topic row |
| `--head-timeout` (30 s) | from accept to the first request's size prefix, and to a finished login when one is required (decision 5); the TLS handshake included | close with no bytes |
| `--idle-timeout` (120 s), `--max-time` (3600 s), `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s; a Fetch's wait is capped below the idle timeout (decision 6) | close with no bytes |

- No new limit option is added: ADR-0006's options and the bus's bounds cover every store a peer can
  fill. The only new option is decision 4's `--kafka-fetch-compression`.
- Every `error_message` the broker writes (Produce v8, SaslAuthenticate) is fixed text from the
  server's own table, never naming a path, an account or an exception (ADR-0006 section 3); where the
  field is nullable it is null except for SaslAuthenticate's.

## Alternatives considered

- **Store compressed batches as produced and serve them unchanged** (BL-521's first planning
  assumption). Rejected: the bus holds one message a record (ADR-0100 decision 3.4), so a record must
  be decoded for an `mqtt` subscriber to see it; a second, Kafka-only copy of the batch would split the
  log in two with two sets of offsets.
- **Serve only uncompressed batches.** Rejected: a consumer's decompression could then never be
  exercised against `surl`, and root `CLAUDE.md` rules out leaving a codec out because the BCL has no
  primitive for it.
- **Answer an unserved API or version with `UNSUPPORTED_VERSION` (35).** Rejected for every API but
  ApiVersions (decision 1): the response would have to be written in a schema that does not exist.
- **Serve the flexible versions too.** Rejected for now: every client negotiates down through
  ApiVersions, the non-flexible ranges cover every version the port's client and Kafka 4 clients
  fall back to, and only ApiVersions v3 is worth its compact types, as the first request a client
  sends.
- **Several partitions a topic.** Rejected by ADR-0100 decision 3.2: the bus's log is one sequence a
  topic.
- **Keep the codecs inside the Kafka server.** Rejected (decision 4): they are protocol-free, sizeable,
  and wanted by HTTP's content coding.

## Consequences

- `surl kafka://127.0.0.1:0/` (with `--allow-anonymous`, or an account and SCRAM) is a one-node Kafka
  broker over the shared bus: Metadata, Produce, Fetch and ListOffsets, auto-created one-partition
  topics, and consumers can be served batches in any of the five codecs.
- A new library, `Foundation/Surl.Compression.UnitLibrary`, holds hand-built Snappy, LZ4 and zstd
  codecs and the XXH32 and XXH64 checksums (BL-702 to BL-699); BL-521 depends on them.
- `Surl.Authentication` gains SCRAM-SHA-512 (BL-700); every server offering SCRAM through the policy
  can then offer it.
- Consumer groups, committed offsets, CreateTopics and InitProducerId wait for BL-701.
- The requirement matrix [`Kafka.md`](../../Conformance/Extended/Kafka.md) traces each server-side
  requirement of the guide to BL-517 to BL-521; each task replaces its ID with the named test.

## Amendment, 2026-11-05 (BL-519)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), while building Produce,
on points decision 4 and the guide leave open:

- **A compressed batch before BL-521** (codec 1 to 4) is `UNSUPPORTED_FOR_MESSAGE_FORMAT` (43), the
  answer the "Record Batch" checks give to a format the broker does not take; BL-521 replaces it with
  decompression. Codec 5 to 7 is `CORRUPT_MESSAGE` (2) as decision 4 says.
- **Null or empty `records`**, a batch whose record count is below 1, a negative header count and a
  null header key are `CORRUPT_MESSAGE` (2): the "Record" layout defines none of them, and each is a
  batch that fails its checks.
- **A failed partition** answers `base_offset`, `log_append_time_ms` and `log_start_offset` (v5) as -1,
  the guide's value for "no offset" and "no time"; decision 3's "never -1" is for a partition that was
  appended.
- **`acks` is checked before `transactional_id`**: an invalid `acks` is 21 even with a transactional id.
- **The topic checks come before the batch checks**: an invalid name (17), then a partition other than
  0 (3), then topic access (29), then the batches, then the record size limit (10), then the bus's
  topic bound (44).
- **All or nothing** holds for every check the broker can make before it publishes; a `StoreFull` met
  by the bus part of the way through a partition keeps the records before it until the bus can append
  a list whole (BL-754).
