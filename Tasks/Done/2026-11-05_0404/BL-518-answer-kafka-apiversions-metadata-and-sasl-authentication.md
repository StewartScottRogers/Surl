---
id: BL-518
title: Answer Kafka ApiVersions, Metadata and SASL authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-517]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-518 — Answer Kafka ApiVersions, Metadata and SASL authentication

## Goal

The Kafka broker answers ApiVersions with its served ranges. It answers Metadata with itself as the only broker
and controller and with the topics and partitions it holds. It runs SaslHandshake and SaslAuthenticate with
`PLAIN` against the accounts, and refuses other requests before login as BL-516's ADR says.

## Context

- Decision: BL-516's ADR, decisions 1, 2 and 5.
- Kafka protocol guide: ApiVersions (v0 to v3; the v0 fallback rule for an unsupported ApiVersions version),
  Metadata v4 (brokers, `cluster_id`, `controller_id`, topics with `is_internal`, partitions with leader, replicas
  and isr, and `allow_auto_topic_creation`), SaslHandshake v1 (`UNSUPPORTED_SASL_MECHANISM` 33 with the enabled
  mechanisms), and SaslAuthenticate v0 (`SASL_AUTHENTICATION_FAILED` 58). RFC 4616 for the `PLAIN` message.
- Logins go through the SASL contracts in `Surl.Protocol.Abstractions`.

## Acceptance criteria

- [x] Named tests show:
      - ApiVersions listing exactly the ADR's ranges;
      - an ApiVersions version above the range answered with v0 and `UNSUPPORTED_VERSION`;
      - Metadata for all topics and for one missing topic (created or `UNKNOWN_TOPIC_OR_PARTITION`, as the ADR says);
      - SaslHandshake for `PLAIN` accepted and for `GSSAPI` refused with 33 and the mechanism list;
      - a good `PLAIN` accepted;
      - a bad one gets 58;
      - Metadata before login handled as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

What was built (2026-11-05, dark factory lane 4):

- `KafkaApiVersionsAnswerer` (v0 to v3), `KafkaMetadataAnswerer` (v0 to v8, over `Surl.MessageBus`),
  `KafkaSaslHandshakeAnswerer`, `KafkaSaslAuthenticateAnswerer`, `KafkaSaslLogin` and the per-connection
  `KafkaConnectionState`. `KafkaBroker` now takes an `IAuthenticationPolicy` and closes any request but
  ApiVersions and the SASL ones before a login, unless the policy lets an anonymous caller in.
  `KafkaRequest` carries the connection state; `KafkaAnswer.RespondThenClose` writes a refused
  SaslAuthenticate response and then closes.
- The acceptance criteria's tests: `KafkaApiVersionsAnswererTests.ServeAsync_ApiVersionsZero_ListsExactlyTheAdrsRanges`;
  `KafkaBrokerTests.ServeAsync_ApiVersionsAboveItsRange_AnswersVersionZeroWithUnsupportedVersionAndEveryRange`
  (BL-517's, unchanged); `KafkaMetadataAnswererTests.ServeAsync_MetadataFourForAllTopics_ListsEveryTopicInOrdinalOrderOfItsKafkaName`,
  `ServeAsync_MetadataFourForAMissingTopicWithAutoCreation_CreatesItWithPartitionZero` and
  `ServeAsync_MetadataFourForAMissingTopicWithoutAutoCreation_IsUnknownTopicOrPartition`;
  `KafkaSaslHandshakeAnswererTests.ServeAsync_HandshakeForPlain_IsAcceptedWithTheOfferedList` and
  `ServeAsync_HandshakeForGssapi_IsUnsupportedSaslMechanismAndASecondHandshakeIsAnswered`;
  `KafkaSaslAuthenticateAnswererTests.ServeAsync_GoodPlainOnVersionZero_IsAcceptedAndMetadataIsServedAfterIt`
  and `ServeAsync_BadPlain_IsSaslAuthenticationFailedThenClosed`;
  `KafkaMetadataAnswererTests.ServeAsync_MetadataBeforeALogin_ClosesWithNoBytesAndAsksThePolicyOnce`.
  Every BL-518 row of `Documentation/Conformance/Extended/Kafka.md` now names its test.
- Gates: `dotnet build -warnaserror` clean; fast tests green (Kafka 198, MessageBus 433);
  `Measure-CodeQuality.ps1` reports the Kafka library at 100% line, 100% branch, 0 failing members, worst
  CRAP 10. `Surl.MessageBus.UnitLibrary` stays 100%/100%; the script flags 7 of its members for complexity
  (`BusMessageCodec`, `NatsSubjectNames`, `MessageBusFiles`, `KafkaTopicNames.IsKafkaTopic`,
  `MqttTopicNames`, `MessageBus.RequireWithinBounds`), all there before this task and none of its code.

Touches widened (rule 3; no task in Doing named them):

- `Surl.MessageBus.UnitLibrary` and `.UnitTests`: Metadata must create a topic's empty log and ask a
  caller's rights without publishing, and the bus could do neither. Added `MessageBus.CreateLog(topic)`
  (`TooManyTopics` past `MaxTopics`, `InvalidTopic` for a non-canonical name) and
  `MessageBus.GetTopicAccess(account, topic)` (the `pub` and `sub` rights, whether or not the topic
  exists), with tests.
- `Documentation/Conformance/Extended/Kafka.md`: ADR-0105 asks each task to replace its ID in the matrix
  with the named test.

Choices made where ADR-0105 leaves room. They are not written into the ADR because
`Documentation/Planning/Decisions` is in BL-423's `touches` while BL-423 is in Doing; a later docs pass may
fold them in:

- SaslHandshake matches mechanism names exactly, since Kafka's names are case-sensitive: `plain` gets 33.
  The policy's offer is matched without regard to case when it is filtered to Kafka's three.
- A SaslHandshake after a mechanism was chosen but before SaslAuthenticate closes with no bytes, like one
  after the login, because Kafka's broker treats it as an illegal state. Another handshake may come only
  after a refused one.
- SCRAM's server-final: `Surl.Authentication` issues it as a second challenge, as the mail servers need.
  A Kafka SCRAM client sends nothing after it, so `KafkaSaslLogin` answers that challenge itself with an
  empty response and returns server-final in the `auth_bytes` of the response that ends the login
  (RFC 4422 section 5's additional data with success, as ADR-0105 decision 5 describes).
- After a v0 handshake an accepted raw token is answered with the server's message as a size-prefixed
  token (four zero bytes for `PLAIN`), as Kafka's broker does; a refusal closes with no bytes.
- The anonymous check (`CheckPasswordLoginAsync` with no user) is asked once a connection, at accept; the
  real policy answers it without the refusal delay. `--head-timeout` runs from accept until the first
  request is complete and, when a login is required, until the login is accepted.
- `--topic-access`: a topic the caller may neither publish nor subscribe to gets 29. Access is checked
  before existence and before creation. v8's operations are DESCRIBE, plus READ for `sub` and WRITE for `pub`.
- An empty log made by Metadata is persisted by the bus's next `SaveChangesAsync`; the answerer does not
  save it, because an empty log holds nothing to lose. BL-522's composition decides when the broker saves.
- `PLAIN`'s `authzid` rule (RFC 4616) is enforced by the policy's `PlainSaslExchange`; the broker passes
  `auth_bytes` to it unchanged.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Kafka broker answers ApiVersions v0-3, Metadata v0-8 from the shared bus, and SaslHandshake/SaslAuthenticate with PLAIN and SCRAM, closing other requests before a login
