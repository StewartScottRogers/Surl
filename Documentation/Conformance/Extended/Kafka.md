# Kafka requirement matrix

The server-side requirements `Surl.Extended.Protocol.Kafka` answers, for the `kafka` and `kafkas`
schemes (ADR-0083 decision 9). Written from the Apache Kafka protocol guide for Kafka 4.4
(<https://kafka.apache.org/44/design/protocol>), the "Record Batch" and "Record" sections of the Kafka
documentation, RFC 4616 and RFC 5802, the specifications
[ADR-0105](../../Planning/Decisions/ADR-0105-how-the-kafka-broker-answers-kafka-and-kafkas.md)
and
[ADR-0145](../../Planning/Decisions/ADR-0145-how-the-kafka-broker-answers-consumer-groups-committed-offsets-createtopics-and-initproducerid.md)
decide by, never from the Curl port's client matrices. Each row binds a server; "Covered by" names
the task that proves it, until that task replaces its ID with the named test, or says why the row
needs no test.

The guide states few requirements in RFC keywords, so each schema field a broker writes or must read,
each rule of a section and each error code a broker owes is a row. IDs are `Kafka:Guide-<Section>-<n>`
for the protocol guide, `Kafka:Batch-<Part>-<n>` for the record format, `Kafka:RFC<n>-<section>-<n>`
for an RFC, and `Kafka:Surl-<Decision>-<n>` for a rule ADR-0105 or ADR-0145 adds where the guide
leaves the broker a choice.

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
| Kafka:Batch-Header-1 | MUST | A batch is read as `baseOffset`, `batchLength`, `partitionLeaderEpoch`, `magic`, `crc`, `attributes`, `lastOffsetDelta`, `baseTimestamp`, `maxTimestamp`, `producerId`, `producerEpoch`, `baseSequence` and the record count; a batch whose length runs past `records` is `CORRUPT_MESSAGE` (2). | `KafkaRecordBatchReaderTests.Read_ABatchLengthPastTheRecords_IsCorruptMessage`, `Read_ABatchLengthShorterThanItsHeader_IsCorruptMessage`, `Read_BytesShorterThanABatchHeaderAfterABatch_IsCorruptMessage`, `Read_TwoBatches_ReturnsEachBatchsProducerFields` |
| Kafka:Batch-Header-2 | MUST | A `magic` other than 2 is `UNSUPPORTED_FOR_MESSAGE_FORMAT` (43). | `KafkaProduceAnswererTests.ServeAsync_MagicOne_IsUnsupportedForMessageFormat` |
| Kafka:Batch-Crc-1 | MUST | The CRC-32C of the bytes from `attributes` to the batch's end must equal `crc`, else `CORRUPT_MESSAGE` (2). | `KafkaProduceAnswererTests.ServeAsync_AFlippedByte_IsCorruptMessageAndNothingIsAppended`, `KafkaRecordBatchReaderTests.Read_ACrcThatDoesNotMatch_IsCorruptMessage`, `KafkaCrc32CTests.Compute_TheCheckString_IsTheCastagnoliCheckValue` |
| Kafka:Batch-Record-1 | MUST | Each record is read as length, attributes, `timestampDelta`, `offsetDelta`, key, value and headers; a count, length or `offsetDelta` that does not agree with the batch is `CORRUPT_MESSAGE` (2). | `KafkaRecordBatchReaderTests.Read_ACountOrLastOffsetDeltaThatDisagrees_IsCorruptMessage`, `Read_FewerRecordsThanTheCount_IsCorruptMessage`, `Read_BytesAfterTheLastRecord_IsCorruptMessage`, `Read_AnOffsetDeltaOutOfPlace_IsCorruptMessage`, `Read_ARecordLengthThatDisagreesWithItsBytes_IsCorruptMessage` |
| Kafka:Batch-Transaction-1 | MUST | A non-null `transactional_id`, or a transactional or control batch, is `UNSUPPORTED_FOR_MESSAGE_FORMAT` (43). | `KafkaProduceAnswererTests.ServeAsync_ATransactionalId_IsUnsupportedForMessageFormat`, `KafkaRecordBatchReaderTests.Read_Attributes_AreAnsweredByTheirBits` |
| Kafka:Surl-Produce-1 | MUST | A failed batch fails its whole partition and nothing of the partition is appended. | `KafkaProduceAnswererTests.ServeAsync_AFlippedByte_IsCorruptMessageAndNothingIsAppended`, `ServeAsync_ARecordPastTheBusMessageLimit_IsMessageTooLarge` (a bus store bound met part of the way through a partition keeps the records before it: BL-754) |
| Kafka:Surl-Produce-2 | MUST | Each record becomes one bus message: value as payload (null as empty), key, UTF-8 headers in order, QoS 1; an `mqtt` subscriber receives it. | `KafkaProduceAnswererTests.ServeAsync_AProducedRecord_IsDeliveredToABusSubscriberOfTheMappedTopic`, `KafkaRecordBatchReaderTests.Read_HeadersWithANullValueOrMalformedUtf8_AreDropped` |
| Kafka:Surl-Produce-3 | MUST | A record past `--max-filesize` is `MESSAGE_TOO_LARGE` (10); a bus store bound is `POLICY_VIOLATION` (44). | `KafkaProduceAnswererTests.ServeAsync_ARecordPastTheBusMessageLimit_IsMessageTooLarge`, `ServeAsync_ANewTopicPastTheBusTopicLimit_IsPolicyViolation`, `ServeAsync_ARecordPastTheBusLogBound_IsPolicyViolation` |

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

## Consumer groups (FindCoordinator v0 to v2, JoinGroup v0 to v5, SyncGroup, Heartbeat and LeaveGroup v0 to v3; error codes 22 to 27, 30, 42, 44, 79, 81, 82)

None of these APIs is served yet: ApiVersions does not list them and each closes the connection
like any unserved API (`Kafka:Surl-Versions-1`). The task named builds each row.

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-Groups-1 | MUST | ApiVersions lists FindCoordinator 0 to 2, JoinGroup 0 to 5, SyncGroup, Heartbeat and LeaveGroup 0 to 3 once served, and never ConsumerGroupHeartbeat (68) or ConsumerGroupDescribe (69). | BL-948 (ADR-0145 decision 1) |
| Kafka:Guide-FindCoordinator-1 | MUST | FindCoordinator for a group (`key_type` 0, or v0) answers node 0 with the host and port Metadata advertises, error 0. | BL-948 (ADR-0145 decision 2) |
| Kafka:Surl-FindCoordinator-1 | MUST | `key_type` 1 (transaction), 2 (share) or any other value is `INVALID_REQUEST` (42), node -1. | BL-948 (ADR-0145 decision 2) |
| Kafka:Guide-JoinGroup-1 | MUST | An empty `group_id` is `INVALID_GROUP_ID` (24); a session timeout outside 6000 to 1800000 `INVALID_SESSION_TIMEOUT` (26); an empty or incompatible `protocol_type` or `protocols` `INCONSISTENT_GROUP_PROTOCOL` (23); an unknown `member_id` `UNKNOWN_MEMBER_ID` (25). | BL-948 (ADR-0145 decision 3) |
| Kafka:Guide-JoinGroup-2 | MUST | On v4 and v5 a dynamic member joining with an empty `member_id` is `MEMBER_ID_REQUIRED` (79) carrying its new id, and joins when it sends that id back. | BL-948 (ADR-0145 decision 3) |
| Kafka:Guide-JoinGroup-3 | MUST | When the join phase ends every waiting JoinGroup is answered with the new generation, the chosen protocol, the leader and its member id; only the leader's answer lists the members and their metadata. | BL-948 (ADR-0145 decision 3) |
| Kafka:Surl-JoinGroup-1 | MUST | The join phase ends when every member has rejoined or the rebalance timeout passes, with no initial delay; the leader stays leader when it rejoins; the protocol is the leader's first that every member lists; member ids are `<client_id or instance id>-<uuid>` from `ExchangeContext.Identifiers`. | BL-948 (ADR-0145 decision 3) |
| Kafka:Guide-StaticMembership-1 | MUST | A JoinGroup naming a held `group_instance_id` fences the old member id: its later requests are `FENCED_INSTANCE_ID` (82). | BL-948 (ADR-0145 decision 3) |
| Kafka:Guide-SyncGroup-1 | MUST | The leader's assignments are relayed byte for byte to each member named, empty bytes to a member not named; followers are held until the leader's SyncGroup arrives. | BL-948 (ADR-0145 decision 3) |
| Kafka:Guide-SyncGroup-2 | MUST | SyncGroup and Heartbeat answer 25 for an unknown member, `ILLEGAL_GENERATION` (22) for another generation, and `REBALANCE_IN_PROGRESS` (27) while the group prepares a rebalance. | BL-948 (ADR-0145 decision 3) |
| Kafka:Guide-Session-1 | MUST | A member with no JoinGroup, SyncGroup, Heartbeat or OffsetCommit within its `session_timeout_ms` is removed and the group rebalances, timed on the injected `TimeProvider`. | BL-948 (ADR-0145 decision 3) |
| Kafka:Guide-LeaveGroup-1 | MUST | LeaveGroup removes the member (v0 to v2) or each member by id or instance id (v3, a per-member error), and the group rebalances or empties. | BL-948 (ADR-0145 decision 3) |
| Kafka:Surl-Groups-1 | MUST | More than 1000 groups is `POLICY_VIOLATION` (44); more than 1000 members of one group `GROUP_MAX_SIZE_REACHED` (81); group state past 256 MiB 44. | BL-948 (ADR-0145 decision 9) |
| Kafka:Surl-Groups-2 | MUST | A held JoinGroup or SyncGroup pauses the exchange's idle clock until it is answered. | BL-948 (ADR-0145 decision 9) |
| Kafka:Surl-Groups-3 | MUST | Joins, leaves, removals, fencing, each new generation and each assignment publish a `ConsumerGroupChangedEvent`. | BL-948 (ADR-0145 decision 10) |

## Committed offsets and group listing (OffsetCommit v2 to v7, OffsetFetch v1 to v5, DescribeGroups v0 to v4, ListGroups v0 to v2; error codes 3, 12, 17, 22, 25, 27, 29, 30, 44)

Not served yet, as above.

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-OffsetCommit-1 | MUST | A member of the current generation commits per partition; a simple commit (generation -1, empty member id) is taken only while the group has no members, else 25; another generation is 22; a commit while the group completes a rebalance is 27. | BL-949 (ADR-0145 decision 4) |
| Kafka:Guide-OffsetCommit-2 | MUST | Each partition is answered: invalid name 17, partition other than 0 or no log 3, no subscribe access 29, metadata over 4096 bytes `OFFSET_METADATA_TOO_LARGE` (12), a bus bound or out-of-space fault 44, otherwise 0. | BL-949 (ADR-0145 decision 4) |
| Kafka:Guide-OffsetFetch-1 | MUST | OffsetFetch answers each partition's committed offset, leader epoch (v5) and metadata, or -1 and empty metadata when none is committed; null `topics` (v2 on) answers every topic the group committed. | BL-949 (ADR-0145 decision 4) |
| Kafka:Surl-Offsets-1 | MUST | Committed offsets are kept in `Surl.MessageBus`, never expire by time, empty on a bus reset, return with a restore, and persist in `.surl/bus/offsets` under `--directory`; at most 10000 entries, a new one past that 44. | `MessageBusCommittedOffsetTests` (stored, replaced, `The10001stNewEntry_IsStoreFullButReplacingAnExistingOneSucceeds`), `MessageBusSeedResetSnapshotTests.Reset_CommittedOffsets_EmptiesTheTable` and `SnapshotChangeRestore_GivesBackTheCommittedOffsetsAndCountsTheirBytes`, `MessageBusFilesTests.SaveChangesAsync_CommittedOffsetsOfTwoGroups_WritesTheOffsetsFilesExactBytesInOrdinalOrder`, `MessageBusPersistenceTests.LoadAsync_CommittedOffsetsSaved_ComeBackEqualAndTheNextSaveWritesNothingNew` (BL-947; the 44 answer BL-949) (ADR-0145 decisions 4 and 9) |
| Kafka:Surl-Offsets-2 | MUST | Each committed partition publishes `StoreChangedEvent` `Committed` named `$group/<group>/<canonical topic>`. | `MessageBusCommittedOffsetTests.CommitOffset_EachCommit_PublishesOneCommittedNamedByTheEncodedGroupAndTheTopic` (BL-947) (ADR-0145 decision 10) |
| Kafka:Guide-DescribeGroups-1 | MUST | DescribeGroups answers each group's state, protocol type and data, and members with their client id, host, metadata and assignment; a group that does not exist is `Dead`. | BL-949 (ADR-0145 decision 5) |
| Kafka:Guide-ListGroups-1 | MUST | ListGroups lists every group with a member or a committed offset that the caller may use, in ordinal order, with its protocol type. | BL-949 (ADR-0145 decision 5) |
| Kafka:Surl-GroupAccess-1 | MUST | Once any `--topic-access` row is given, a group needs `sub` on `$group/<id>`; without it every group API is `GROUP_AUTHORIZATION_FAILED` (30), the same whether or not the group exists, and ListGroups leaves it out. | `TopicAccessPolicyTests.MayUseGroup_DollarGroup_IsGrantedByALiteralOrPlusSubGrantNeverByHashOrPubAndOpenWithNoGrants` (BL-947), BL-948 and BL-949 (ADR-0145 decision 6) |

## CreateTopics (CreateTopics v2 to v4; error codes 17, 29, 36 to 39, 42, 44)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-CreateTopics-1 | MUST | CreateTopics creates each topic's empty log on the bus, one partition, and answers each name in request order with its error code and a null message. | `KafkaCreateTopicsAnswererTests.ServeAsync_NewTopic_IsCreatedWithAnEmptyLogAndListedByMetadata`, `ServeAsync_VersionFourWithMinusOnePartitionsAndReplicationFactor_CreatesTheTopic`, `KafkaApiVersionsAnswererTests.ServeAsync_ApiVersionsZero_ListsCreateTopicsTwoToFour` |
| Kafka:Guide-CreateTopics-2 | MUST | A duplicate name is `INVALID_REQUEST` (42); an invalid name 17; no publish access 29; a topic with a log `TOPIC_ALREADY_EXISTS` (36); `num_partitions` other than 1 or -1 `INVALID_PARTITIONS` (37) (ADR-0100 decision 3.2); `replication_factor` other than 1 or -1 `INVALID_REPLICATION_FACTOR` (38); any assignment but partition 0 on broker 0 `INVALID_REPLICA_ASSIGNMENT` (39); the bus's topic bound 44. | `KafkaCreateTopicsAnswererTests.ServeAsync_EachInvalidTopic_IsAnsweredItsCodeWhileTheOthersAreCreated`, `ServeAsync_TopicTheCallerMayNotPublishTo_IsTopicAuthorizationFailed`, `ServeAsync_TopicPastTheBussMostTopics_IsPolicyViolation` |
| Kafka:Guide-CreateTopics-3 | MUST | `validate_only` makes the checks and creates nothing. | `KafkaCreateTopicsAnswererTests.ServeAsync_ValidateOnly_AnswersZeroAndCreatesNothing` |
| Kafka:Surl-CreateTopics-1 | MUST | Topic `configs` are accepted and ignored, the ignored names noted with `-v`. | `KafkaCreateTopicsAnswererTests.ServeAsync_TopicWithConfigs_IsCreatedAndTheIgnoredNamesNoted` |
| Kafka:Surl-CreateTopics-2 | MUST | Each topic asks the fault plan once, command `CreateTopics`, path its Kafka name: an `error` rule's code 1 to 127 is that topic's `error_code` and the topic is not created, other replies are noted and not applied; a `drop` aborts with no answer. | `KafkaCreateTopicsAnswererTests.ServeAsync_ErrorRuleOnOneTopic_AnswersItsCodeAndLeavesThatTopicAbsent`, `ServeAsync_ErrorRuleWhoseReplyIsNotACodeOneTo127_IsNotAppliedAndNoted`, `ServeAsync_DropRule_AbortsWithNoBytesAndCreatesNothing` |

## InitProducerId and idempotent producers (InitProducerId v0 to v1, Produce; error codes 42, 45, 47)

`KafkaInitProducerIdAnswerer` hands out producer ids, and `KafkaProducerStates`, which it shares
with `KafkaProduceAnswerer`, checks each idempotent batch's sequence. A batch with `producerId` -1
is not checked (`KafkaProduceAnswererTests.ServeAsync_ProducerIdMinusOne_IsNeverCheckedAndARepeatIsAppendedAgain`).

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-InitProducerId-1 | MUST | A null `transactional_id` is answered error 0, a producer id unique on the instance and rising by one from a start drawn from `ExchangeContext.Identifiers`, and epoch 0. | `KafkaInitProducerIdAnswererTests.ServeAsync_TwoCallsWithANullTransactionalId_AnswerConsecutiveIdsFromTheIdentifierStartAndEpochZero`, `KafkaProducerStatesTests.NextProducerId_AtTheLargest47BitId_WrapsToZero`, `KafkaApiVersionsAnswererTests.ServeAsync_ApiVersionsZero_ListsInitProducerIdZeroToOne` |
| Kafka:Surl-Producer-1 | MUST | A non-null `transactional_id` is `INVALID_REQUEST` (42) with producer id and epoch -1. | `KafkaInitProducerIdAnswererTests.ServeAsync_ATransactionalIdEvenEmpty_IsInvalidRequestWithMinusOneIdAndEpoch` |
| Kafka:Surl-Producer-2 | MUST | A batch repeating one of the producer's last five batches on the topic is answered 0 with its original base offset, `log_append_time_ms` -1, and nothing appended; a partition mixing repeated and new batches is `OUT_OF_ORDER_SEQUENCE_NUMBER` (45). | `KafkaProduceAnswererTests.ServeAsync_AnIdempotentBatchThenItsExactRetry_AnswersTheSameBaseOffsetAndTheLogHoldsItOnce`, `ServeAsync_TwoBatchesInOnePartitionThenTheirRetry_AreAppendedOnceAndTheRetryAnswersTheFirstBaseOffset`, `ServeAsync_ARetriedBatchFollowedByANewOneInOnePartition_IsOutOfOrderSequenceNumberAndNothingIsAppended`, `KafkaProducerStatesTests.Check_ARepeatOfTheSixthBatchBack_IsOutOfOrderAndOfTheFifthIsADuplicate` |
| Kafka:Surl-Producer-3 | MUST | A sequence that is not one past the last taken is `OUT_OF_ORDER_SEQUENCE_NUMBER` (45); an older epoch `INVALID_PRODUCER_EPOCH` (47); a producer with no state held is accepted at any sequence. | `KafkaProduceAnswererTests.ServeAsync_ABatchAfterASequenceGap_IsOutOfOrderSequenceNumberAndNothingIsAppended`, `ServeAsync_ABatchOfAnOlderEpoch_IsInvalidProducerEpoch`, `ServeAsync_ANewerEpoch_IsTakenAtAnySequence`, `ServeAsync_AProducerWithNoStateAtSequenceSeven_IsAccepted`, `ServeAsync_Sequence2147483647ThenZero_AreBothAccepted` |
| Kafka:Surl-Producer-4 | MUST | At most 10000 producer states are held, the least recently used dropped. | `KafkaProducerStatesTests.Record_The10001stProducerState_DropsTheLeastRecentlyUsedWhoseNextBatchIsThenAcceptedAtAnySequence` |
| Kafka:Surl-Producer-5 | MUST | An `error` fault rule on command `InitProducerId` is the top-level `error_code` with producer id and epoch -1; a `drop` aborts with no answer. | `KafkaInitProducerIdAnswererTests.ServeAsync_ErrorRule_AnswersItsCodeWithMinusOneAndDrawsNoId`, `ServeAsync_ErrorRuleWhoseReplyIsNotACodeOneTo127_IsNotAppliedAndNoted`, `ServeAsync_DropRule_AbortsWithNoBytes` |

## Not required of this broker

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Kafka:Guide-Transactions-1 | MUST | Transactions (a transactional InitProducerId, AddPartitionsToTxn, EndTxn, TxnOffsetCommit) and READ_COMMITTED filtering of aborted transactions. | n/a: no transaction coordinator (ADR-0105 decision 4); a transactional id is refused (ADR-0145 decisions 2 and 8) |
| Kafka:Guide-ConsumerProtocol-1 | MUST | The KIP-848 consumer group protocol (ConsumerGroupHeartbeat, ConsumerGroupDescribe), share groups and streams groups. | n/a: not served; the classic group protocol is (ADR-0145 decision 1) |
| Kafka:Guide-Replication-1 | MUST | Replication, leader election and the `acks` -1 wait for the in-sync replicas. | n/a: one node, its own only replica (ADR-0105 decisions 0 and 6) |
| Kafka:Guide-Flexible-1 | MUST | The flexible versions of Produce, Fetch, ListOffsets, Metadata and SaslAuthenticate. | n/a: not served (ADR-0105 decision 1); clients negotiate down through ApiVersions |
