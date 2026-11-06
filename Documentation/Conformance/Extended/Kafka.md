# Kafka requirement matrix

The server-side requirements `Surl.Extended.Protocol.Kafka` answers, for the `kafka` and `kafkas`
schemes (ADR-0083 decision 9). Written from the Apache Kafka protocol guide for Kafka 4.4
(<https://kafka.apache.org/44/design/protocol>), the "Record Batch" and "Record" sections of the Kafka
documentation, RFC 4616 and RFC 5802, the specifications
[ADR-0105](../../Planning/Decisions/ADR-0105-how-the-kafka-broker-answers-kafka-and-kafkas.md)
decides by, never from the Curl port's client matrices. Each row binds a server; "Covered by" names
the task that proves it, until that task replaces its ID with the named test, or says why the row
needs no test.

The guide states few requirements in RFC keywords, so each schema field a broker writes or must read,
each rule of a section and each error code a broker owes is a row. IDs are `Kafka:Guide-<Section>-<n>`
for the protocol guide, `Kafka:Batch-<Part>-<n>` for the record format, `Kafka:RFC<n>-<section>-<n>`
for an RFC, and `Kafka:Surl-<Decision>-<n>` for a rule ADR-0105 adds where the guide leaves the
broker a choice.

## Framing, headers and primitive types (guide: "Network", "Protocol Primitive Types", "Common Request and Response Structure", "Request and Response Headers")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-Framing-1 | MUST | A request is read as an INT32 size and that many bytes; a response is written the same way. | BL-517 |
| Kafka:Guide-Framing-2 | MUST | Requests on one connection are processed in the order sent and answered in that order. | BL-517 |
| Kafka:Guide-Framing-3 | MUST | Every response carries the `correlation_id` of its request. | BL-517 |
| Kafka:Guide-Header-1 | MUST | A non-flexible request is read with request header v1 (`api_key`, `api_version`, `correlation_id`, `client_id` NULLABLE_STRING) and answered with response header v0. | BL-517 |
| Kafka:Guide-Header-2 | MUST | A flexible request (ApiVersions v3) is read with request header v2, its tagged fields read and skipped; ApiVersions' response keeps response header v0. | BL-517 |
| Kafka:Guide-Types-1 | MUST | BOOLEAN, INT8, INT16, INT32, INT64 and UINT32 are big-endian; a BOOLEAN is 0 or 1. | BL-517 |
| Kafka:Guide-Types-2 | MUST | VARINT and VARLONG are zig-zag base-128; UNSIGNED_VARINT is plain base-128. | BL-517 |
| Kafka:Guide-Types-3 | MUST | STRING and NULLABLE_STRING are an INT16 length (-1 for null) and UTF-8 bytes; BYTES and NULLABLE_BYTES an INT32 length (-1 for null) and bytes; ARRAY an INT32 count (-1 for null) and elements. | BL-517 |
| Kafka:Guide-Types-4 | MUST | COMPACT_STRING, COMPACT_ARRAY and the tagged-fields section, as ApiVersions v3 uses them (length plus one as UNSIGNED_VARINT). | BL-517 |
| Kafka:Guide-Types-5 | MUST | A request whose fields run past its size, or whose size leaves bytes unread, is not a request; the connection is closed (ADR-0105 decision 7). | BL-517 |
| Kafka:Surl-Versions-1 | MUST | An API key not served, or a served API at a version outside ADR-0105 decision 1's range, closes the connection with no bytes. | BL-517 |
| Kafka:Surl-Limits-1 | MUST | A size prefix above `--max-message`, zero or negative closes the connection with no bytes, the body unread. | BL-517 |
| Kafka:Surl-Limits-2 | MUST | No request size prefix by `--head-timeout` after accept closes the connection with no bytes. | BL-517 |
| Kafka:Guide-Tls-1 | MUST | `kafkas` carries the same protocol inside implicit TLS (TLS 1.2 or 1.3) from the first byte. | n/a: the TLS wrapper is the composition's `ImplicitTlsSchemeServer` (ADR-0010), added by BL-522, not the broker's |

## ApiVersions (guide: "Retrieving Supported API versions"; ApiVersions v0 to v3)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-ApiVersions-1 | MUST | ApiVersions lists each served API key with its minimum and maximum version, and no other. | `KafkaApiVersionsAnswererTests.ServeAsync_ApiVersionsZero_ListsExactlyTheAdrsRanges`, `ServeAsync_ApiVersionsOneAndTwo_AddThrottleTimeZero` |
| Kafka:Guide-ApiVersions-2 | MUST | An ApiVersions version above the highest served is answered by a v0 ApiVersions response with `UNSUPPORTED_VERSION` (35) and the full `api_versions` array. | `KafkaBrokerTests.ServeAsync_ApiVersionsAboveItsRange_AnswersVersionZeroWithUnsupportedVersionAndEveryRange` |
| Kafka:Guide-ApiVersions-3 | MUST | ApiVersions is answered before authentication. | `KafkaApiVersionsAnswererTests.ServeAsync_ApiVersionsBeforeALogin_IsAnswered` |
| Kafka:Guide-ApiVersions-4 | MUST | v3's `finalized_features` and `supported_features` are written (empty), with `zk_migration_ready` false. | `KafkaApiVersionsAnswererTests.ServeAsync_ApiVersionsThree_AnswersTheCompactFormUnderResponseHeaderZero` (no tagged field written: each takes its default, empty or false) |

## Metadata (Metadata v0 to v8; error codes 3, 17, 29)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-Metadata-1 | MUST | `brokers` lists node 0 with the host and port the client reached; `rack` null. | `KafkaMetadataAnswererTests.ServeAsync_MetadataFourForAllTopics_ListsEveryTopicInOrdinalOrderOfItsKafkaName`, `AdvertisedHost_WildcardListener_IsTheConnectionsLocalAddress` |
| Kafka:Guide-Metadata-2 | MUST | `controller_id` (v1) is 0, `cluster_id` (v2) a fixed 22-character id, `is_internal` (v1) false. | `KafkaMetadataAnswererTests.ServeAsync_MetadataFourForAllTopics_ListsEveryTopicInOrdinalOrderOfItsKafkaName`, `ClusterId_Is22Base64UrlCharactersAndDiffersPerAnswerer` |
| Kafka:Guide-Metadata-3 | MUST | Each topic has partition 0 whose leader, replicas and isr are node 0, `offline_replicas` (v5) empty and `leader_epoch` (v7) 0. | `KafkaMetadataAnswererTests.ServeAsync_MetadataEightAskingForOperations_AnswersTheCallersRights` |
| Kafka:Guide-Metadata-4 | MUST | A null topic list (v1 on), or an empty one in v0, lists every topic Kafka can name, in ordinal order. | `KafkaMetadataAnswererTests.ServeAsync_MetadataFourForAllTopics_ListsEveryTopicInOrdinalOrderOfItsKafkaName`, `ServeAsync_MetadataZeroWithAnEmptyArray_ListsEveryTopicAndCreatesNamedOnes`, `ServeAsync_MetadataOneWithAnEmptyArray_ListsNoTopic` |
| Kafka:Guide-Metadata-5 | MUST | A topic that does not exist is created when auto-creation is allowed (always in v0 to v3; by `allow_auto_topic_creation` from v4), else answered `UNKNOWN_TOPIC_OR_PARTITION` (3). | `KafkaMetadataAnswererTests.ServeAsync_MetadataFourForAMissingTopicWithAutoCreation_CreatesItWithPartitionZero`, `ServeAsync_MetadataFourForAMissingTopicWithoutAutoCreation_IsUnknownTopicOrPartition`, `ServeAsync_AutoCreationPastTheBussMostTopics_IsPolicyViolation` |
| Kafka:Guide-Metadata-6 | MUST | A topic name outside `[A-Za-z0-9._-]{1,249}`, or `.` or `..`, is answered `INVALID_TOPIC_EXCEPTION` (17). | `KafkaMetadataAnswererTests.ServeAsync_InvalidTopicName_IsInvalidTopicException` |
| Kafka:Guide-Metadata-7 | MUST | A topic the caller may not access is answered `TOPIC_AUTHORIZATION_FAILED` (29) and left out of an all-topics answer (ADR-0102 decision 3.4). | `KafkaMetadataAnswererTests.ServeAsync_TopicTheCallerMayNotUse_IsTopicAuthorizationFailedAndLeftOutOfTheListing` |
| Kafka:Guide-Metadata-8 | MUST | v8's authorized operations are `-2147483648` unless requested, and when requested the bits ADR-0105 decision 2 names. | `KafkaMetadataAnswererTests.ServeAsync_MetadataEightAskingForOperations_AnswersTheCallersRights`, `ServeAsync_MetadataEightNotAskingForOperations_AnswersNotRequested` |
| Kafka:Surl-Topics-1 | MUST | A bus topic Kafka cannot name (ADR-0100 decision 3.2) is never listed. | `KafkaMetadataAnswererTests.ServeAsync_MetadataFourForAllTopics_ListsEveryTopicInOrdinalOrderOfItsKafkaName` |

## SASL (guide: "SASL Authentication Sequence", SaslHandshake v0 and v1, SaslAuthenticate v0 and v1; RFC 4616; RFC 5802)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-Sasl-1 | MUST | Without `--allow-anonymous`, any request but ApiVersions and SaslHandshake before a finished login closes the connection with no bytes. | `KafkaMetadataAnswererTests.ServeAsync_MetadataBeforeALogin_ClosesWithNoBytesAndAsksThePolicyOnce` |
| Kafka:Guide-Sasl-2 | MUST | SaslHandshake answers the enabled mechanisms; a mechanism not enabled is `UNSUPPORTED_SASL_MECHANISM` (33) and the connection stays open. | `KafkaSaslHandshakeAnswererTests.ServeAsync_HandshakeForPlain_IsAcceptedWithTheOfferedList`, `ServeAsync_HandshakeForGssapi_IsUnsupportedSaslMechanismAndASecondHandshakeIsAnswered` |
| Kafka:Guide-Sasl-3 | MUST | After a v1 handshake the mechanism's messages travel in SaslAuthenticate `auth_bytes`, both ways. | `KafkaSaslAuthenticateAnswererTests.ServeAsync_GoodPlainOnVersionZero_IsAcceptedAndMetadataIsServedAfterIt`, `KafkaSaslLoginTests.ServeAsync_ScramLogin_SendsServerFirstThenServerFinalWithTheSuccess` |
| Kafka:Guide-Sasl-4 | MUST | After a v0 handshake they travel as raw size-prefixed tokens, both ways, and a failure closes the connection. | `KafkaSaslAuthenticateAnswererTests.ServeAsync_GoodPlainInARawTokenAfterAVersionZeroHandshake_IsAnsweredWithAnEmptyToken`, `ServeAsync_ChallengeInARawToken_IsSentAsARawToken`, `ServeAsync_BadPlainInARawToken_ClosesWithNoBytes` |
| Kafka:Guide-Sasl-5 | MUST | A refused login is `SASL_AUTHENTICATION_FAILED` (58) with a fixed `error_message`, after the policy's delay, then a close. | `KafkaSaslAuthenticateAnswererTests.ServeAsync_BadPlain_IsSaslAuthenticationFailedThenClosed` (the delay is the policys) |
| Kafka:Guide-Sasl-6 | MUST | SaslAuthenticate v1's `session_lifetime_ms` is 0. | `KafkaSaslAuthenticateAnswererTests.ServeAsync_GoodPlainOnVersionOne_AddsSessionLifetimeZero` |
| Kafka:Surl-Sasl-1 | MUST | The mechanisms offered are the policy's for the connection, kept to `PLAIN`, `SCRAM-SHA-256` and `SCRAM-SHA-512`; `PLAIN` is not offered on `kafka://` without `--allow-plaintext-auth`. | `KafkaSaslHandshakeAnswererTests.ServeAsync_PolicyOffers_AreKeptToKafkasMechanismsInKafkasOrder` (withholding `PLAIN` without TLS is the policys) |
| Kafka:Surl-Sasl-2 | MUST | SaslAuthenticate without a handshake, or a second handshake after a login, closes the connection with no bytes. | `KafkaSaslAuthenticateAnswererTests.ServeAsync_SaslAuthenticateWithoutAHandshake_ClosesWithNoBytes`, `KafkaSaslHandshakeAnswererTests.ServeAsync_HandshakeAfterTheLogin_ClosesWithNoBytes` |
| Kafka:Surl-Sasl-3 | MUST | A login must finish within `--head-timeout` of accept, else the connection closes with no bytes. | `KafkaBrokerTests.ServeAsync_LoginNotFinishedWithinTheHeadTimeout_ClosesWithNoBytes`, `ServeAsync_LoginFinished_IsNoLongerBoundByTheHeadTimeout` |
| Kafka:RFC4616-2-1 | MUST | The `PLAIN` message is `authzid NUL authcid NUL passwd`; an `authzid` other than empty or the `authcid` is refused. | The policys `PlainSaslExchange` (`Surl.Authentication`); the broker passes `auth_bytes` to it unchanged (`KafkaSaslAuthenticateAnswererTests.ServeAsync_GoodPlainOnVersionOne_AddsSessionLifetimeZero`) |
| Kafka:RFC5802-5-1 | MUST | SCRAM's client-first, server-first, client-final and server-final messages are exchanged in turn, the server proving the password it holds. | `KafkaSaslLoginTests.ServeAsync_ScramLogin_SendsServerFirstThenServerFinalWithTheSuccess`; the proof is the policys SCRAM exchange |
| Kafka:Surl-Sasl-4 | MUST | Under `--allow-anonymous` every request is served without a login, and a login asked for is run and accepted unchecked. | `KafkaMetadataAnswererTests.ServeAsync_AnonymousAllowed_ServesMetadataAndAsksThePolicyOnceAConnection`, `KafkaSaslLoginTests.ServeAsync_LoginAcceptedUnchecked_AuthenticatesWithNoAccount` |

## Produce and record batches (Produce v3 to v8; "Record Batch", "Record"; error codes 2, 3, 10, 17, 21, 29, 43, 44)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-Produce-1 | MUST | Produce reads `transactional_id`, `acks`, `timeout_ms` and, per topic and partition, the records. | `KafkaProduceAnswererTests.ServeAsync_TwoPartitionsOfOneTopic_AreEachAnsweredInTheOrderNamed`, `ServeAsync_NullTopicData_AnswersNoTopics` |
| Kafka:Guide-Produce-2 | MUST | Each partition is answered with its error code, `base_offset` (the first record's offset), `log_append_time_ms` (v2 on) and `log_start_offset` (v5 on); `record_errors` empty and `error_message` null (v8). | `KafkaProduceAnswererTests.ServeAsync_ABatchOfThreeThenABatchOfOne_AreAppendedAtOffsetZeroThenThree`, `ServeAsync_VersionEight_AnswersTheLogStartOffsetEmptyRecordErrorsAndANullErrorMessage`, `ServeAsync_VersionFiveForAFailedPartition_AnswersMinusOneAsTheLogStartOffset` |
| Kafka:Guide-Produce-3 | MUST | `acks` 0 writes no response. | `KafkaProduceAnswererTests.ServeAsync_AcksZero_WritesNoResponseAndAppends`, `ServeAsync_AcksZeroWithAFailedPartition_WritesNoResponseAndNotesTheError` |
| Kafka:Guide-Produce-4 | MUST | `acks` 1 and -1 answer after the records are appended. | `KafkaProduceAnswererTests.ServeAsync_ABatchOfThreeThenABatchOfOne_AreAppendedAtOffsetZeroThenThree` (acks -1, then 1) |
| Kafka:Guide-Produce-5 | MUST | Any other `acks` is `INVALID_REQUIRED_ACKS` (21) for every partition, nothing appended. | `KafkaProduceAnswererTests.ServeAsync_AcksOtherThanZeroOneOrMinusOne_IsInvalidRequiredAcksAndNothingIsAppended` |
| Kafka:Guide-Produce-6 | MUST | A partition other than 0 is `UNKNOWN_TOPIC_OR_PARTITION` (3); an invalid name `INVALID_TOPIC_EXCEPTION` (17); a topic the caller may not publish to `TOPIC_AUTHORIZATION_FAILED` (29). | `KafkaProduceAnswererTests.ServeAsync_AnUnknownPartition_IsUnknownTopicOrPartition`, `ServeAsync_AnInvalidTopicName_IsInvalidTopicException`, `ServeAsync_ATopicTheCallerMayNotPublishTo_IsTopicAuthorizationFailed` |
| Kafka:Guide-Produce-7 | MUST | A Produce to a topic that does not exist creates it. | `KafkaProduceAnswererTests.ServeAsync_ABatchOfThreeThenABatchOfOne_AreAppendedAtOffsetZeroThenThree` |
| Kafka:Batch-Header-1 | MUST | A batch is read as `baseOffset`, `batchLength`, `partitionLeaderEpoch`, `magic`, `crc`, `attributes`, `lastOffsetDelta`, `baseTimestamp`, `maxTimestamp`, `producerId`, `producerEpoch`, `baseSequence` and the record count; a batch whose length runs past `records` is `CORRUPT_MESSAGE` (2). | `KafkaRecordBatchReaderTests.Read_ABatchLengthPastTheRecords_IsCorruptMessage`, `Read_ABatchLengthShorterThanItsHeader_IsCorruptMessage`, `Read_BytesShorterThanABatchHeaderAfterABatch_IsCorruptMessage` |
| Kafka:Batch-Header-2 | MUST | A `magic` other than 2 is `UNSUPPORTED_FOR_MESSAGE_FORMAT` (43). | `KafkaProduceAnswererTests.ServeAsync_MagicOne_IsUnsupportedForMessageFormat` |
| Kafka:Batch-Crc-1 | MUST | The CRC-32C of the bytes from `attributes` to the batch's end must equal `crc`, else `CORRUPT_MESSAGE` (2). | `KafkaProduceAnswererTests.ServeAsync_AFlippedByte_IsCorruptMessageAndNothingIsAppended`, `KafkaRecordBatchReaderTests.Read_ACrcThatDoesNotMatch_IsCorruptMessage`, `KafkaCrc32CTests.Compute_TheCheckString_IsTheCastagnoliCheckValue` |
| Kafka:Batch-Record-1 | MUST | Each record is read as length, attributes, `timestampDelta`, `offsetDelta`, key, value and headers; a count, length or `offsetDelta` that does not agree with the batch is `CORRUPT_MESSAGE` (2). | `KafkaRecordBatchReaderTests.Read_ACountOrLastOffsetDeltaThatDisagrees_IsCorruptMessage`, `Read_FewerRecordsThanTheCount_IsCorruptMessage`, `Read_BytesAfterTheLastRecord_IsCorruptMessage`, `Read_AnOffsetDeltaOutOfPlace_IsCorruptMessage`, `Read_ARecordLengthThatDisagreesWithItsBytes_IsCorruptMessage` |
| Kafka:Batch-Transaction-1 | MUST | A non-null `transactional_id`, or a transactional or control batch, is `UNSUPPORTED_FOR_MESSAGE_FORMAT` (43). | `KafkaProduceAnswererTests.ServeAsync_ATransactionalId_IsUnsupportedForMessageFormat`, `KafkaRecordBatchReaderTests.Read_Attributes_AreAnsweredByTheirBits` |
| Kafka:Surl-Produce-1 | MUST | A failed batch fails its whole partition and nothing of the partition is appended. | `KafkaProduceAnswererTests.ServeAsync_AFlippedByte_IsCorruptMessageAndNothingIsAppended`, `ServeAsync_ARecordPastTheBusMessageLimit_IsMessageTooLarge` (a bus store bound met part of the way through a partition keeps the records before it: BL-754) |
| Kafka:Surl-Produce-2 | MUST | Each record becomes one bus message: value as payload (null as empty), key, UTF-8 headers in order, QoS 1; an `mqtt` subscriber receives it. | `KafkaProduceAnswererTests.ServeAsync_AProducedRecord_IsDeliveredToABusSubscriberOfTheMappedTopic`, `KafkaRecordBatchReaderTests.Read_HeadersWithANullValueOrMalformedUtf8_AreDropped` |
| Kafka:Surl-Produce-3 | MUST | A record past `--max-filesize` is `MESSAGE_TOO_LARGE` (10); a bus store bound is `POLICY_VIOLATION` (44). | `KafkaProduceAnswererTests.ServeAsync_ARecordPastTheBusMessageLimit_IsMessageTooLarge`, `ServeAsync_ANewTopicPastTheBusTopicLimit_IsPolicyViolation`, `ServeAsync_ARecordPastTheBusLogBound_IsPolicyViolation` |
| Kafka:Batch-Producer-1 | MAY | A batch with a `producerId` (an idempotent producer outside a transaction) is accepted and its sequence not checked. | `KafkaRecordBatchReaderTests.Read_ABatchOfThreeRecords_ReturnsEachKeyValueAndHeaderInOrder` (its batch carries `producerId` -1; no field after `attributes` but the record count and `lastOffsetDelta` is read) |

## Fetch and ListOffsets (Fetch v4 to v11, ListOffsets v1 to v5; error codes 1, 3, 17, 29, 70)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-Fetch-1 | MUST | Fetch reads `replica_id`, `max_wait_ms`, `min_bytes`, `max_bytes`, `isolation_level` and, per partition, `fetch_offset` and `partition_max_bytes` (and the later versions' fields). | BL-520 |
| Kafka:Guide-Fetch-2 | MUST | Each partition is answered with `high_watermark` and `last_stable_offset` (both the log end offset), `log_start_offset` (v5), an empty `aborted_transactions`, `preferred_read_replica` -1 (v11) and the records. | BL-520 |
| Kafka:Guide-Fetch-3 | MUST | The records are whole batches starting at `fetch_offset`, within `partition_max_bytes` and `max_bytes`, except that the response's first batch is returned whole (KIP-74). | BL-520 |
| Kafka:Guide-Fetch-4 | MUST | A `fetch_offset` below the log start or above the log end is `OFFSET_OUT_OF_RANGE` (1); at the log end it is an empty success. | BL-520 |
| Kafka:Guide-Fetch-5 | MUST | Fewer than `min_bytes` available waits until enough arrive or `max_wait_ms` passes (capped at 60000), on the injected `TimeProvider`. | BL-520 |
| Kafka:Guide-Fetch-6 | MUST | A Fetch of an unknown topic or partition is `UNKNOWN_TOPIC_OR_PARTITION` (3) and creates nothing; an invalid name 17; no access 29. | BL-520 |
| Kafka:Guide-Fetch-7 | MUST | Every response's `session_id` is 0; a request continuing a session is `FETCH_SESSION_ID_NOT_FOUND` (70). | BL-520 |
| Kafka:Batch-Serve-1 | MUST | A served batch has magic 2, LogAppendTime, `producerId` -1, `producerEpoch` -1, `baseSequence` -1, at most 1000 records, and a correct CRC-32C. | BL-520 |
| Kafka:Batch-Serve-2 | MUST | A served record carries the bus message's offset, publish time, key, payload and headers. | BL-520 |
| Kafka:Surl-Fetch-1 | MUST | A message published over another protocol is fetchable at its log offset. | BL-520 |
| Kafka:Surl-Fetch-2 | MUST | `max_bytes` is capped at `--max-message`, a first batch above it still returned whole. | BL-520 |
| Kafka:Guide-ListOffsets-1 | MUST | Timestamp -2 answers the log start offset, -1 the log end offset. | BL-520 |
| Kafka:Guide-ListOffsets-2 | MUST | Another timestamp answers the first offset published at or after it, or offset -1 and timestamp -1. | BL-520 |
| Kafka:Guide-ListOffsets-3 | MUST | A partition other than 0, an unknown topic, an invalid name or no access is answered 3, 3, 17 or 29. | BL-520 |

## Compressed batches ("Record Batch" attributes; RFC 1952; Snappy; LZ4; RFC 8878)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Batch-Compression-1 | MUST | The attributes' low three bits name the codec: 0 none, 1 gzip, 2 snappy, 3 lz4, 4 zstd; 5 to 7 are `CORRUPT_MESSAGE` (2). | BL-521 |
| Kafka:Batch-Compression-2 | MUST | The batch header stays uncompressed and the CRC-32C covers the compressed bytes as sent. | BL-521 |
| Kafka:Batch-Compression-3 | MUST | A produced gzip, Snappy (raw or xerial), LZ4 or zstd batch is decoded and each record stored as a bus message. | BL-521 |
| Kafka:Batch-Compression-4 | MUST | A payload its codec cannot decode, or whose LZ4 or zstd checksum fails, is `CORRUPT_MESSAGE` (2). | BL-521 |
| Kafka:Surl-Compression-1 | MUST | Decompressed records past the decompression bound are `MESSAGE_TOO_LARGE` (10), decoding stopped. | BL-521 |
| Kafka:Surl-Compression-2 | MUST | Under `--kafka-fetch-compression <codec>` served batches are compressed with that codec and name it in their attributes. | BL-521 |
| Kafka:Surl-Compression-3 | MUST | `--kafka-fetch-compression` exists with its argument type, help and `--aihelp` facts. | n/a: the option is the command line's, added and tested by BL-522; the broker takes the codec as a constructor parameter (BL-521) |
| Kafka:RFC1952-2.3-1 | MUST | A gzip member is read and written as RFC 1952 says (the BCL's `GZipStream`). | BL-521 |
| Kafka:RFC8878-3.1.1-1 | MUST | A zstd frame with a dictionary id the broker does not hold cannot be decoded (`CORRUPT_MESSAGE`). | BL-521 |

## Not required of this broker

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-Groups-1 | MUST | Consumer group coordination and committed offsets (FindCoordinator to OffsetFetch). | n/a: not served yet; BL-701 decides them |
| Kafka:Guide-CreateTopics-1 | MUST | CreateTopics, and `INVALID_PARTITIONS` (37) for more than one partition. | n/a: not served yet; BL-701 decides it |
| Kafka:Guide-Transactions-1 | MUST | InitProducerId, transactions and READ_COMMITTED filtering of aborted transactions. | n/a: no transaction coordinator (ADR-0105 decision 4); BL-701 decides InitProducerId |
| Kafka:Guide-Replication-1 | MUST | Replication, leader election and the `acks` -1 wait for the in-sync replicas. | n/a: one node, its own only replica (ADR-0105 decisions 0 and 6) |
| Kafka:Guide-Flexible-1 | MUST | The flexible versions of Produce, Fetch, ListOffsets, Metadata and SaslAuthenticate. | n/a: not served (ADR-0105 decision 1); clients negotiate down through ApiVersions |
