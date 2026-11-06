---
id: BL-950
title: Answer Kafka CreateTopics on the message bus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-950 — Answer Kafka CreateTopics on the message bus

## Goal

The Kafka broker answers CreateTopics v2-v4 by giving each valid, new topic its empty one-partition log on
`Surl.MessageBus`, and ApiVersions lists CreateTopics.

## Context

- Decision: [ADR-0145](../../Documentation/Planning/Decisions/ADR-0145-how-the-kafka-broker-answers-consumer-groups-committed-offsets-createtopics-and-initproducerid.md) decisions 1, 7 and 10; ADR-0100 decision 3.2 (`INVALID_PARTITIONS` (37) for more
  than one partition). Specification: the Apache Kafka 4.4 protocol guide
  (<https://kafka.apache.org/44/design/protocol>), CreateTopics and the "Error Codes" table.
- Checks per topic, in request order: a duplicate name 42 (each occurrence), an invalid Kafka name 17, no publish
  access to the canonical topic 29 (`MessageBus.GetTopicAccess`), a topic with a log 36, `num_partitions` other than
  1 or -1 37, `replication_factor` other than 1 or -1 38, assignments other than exactly partition 0 on `[0]` 39, then
  `MessageBus.CreateLog` (`TooManyTopics` 44). `validate_only` checks everything but `CreateLog`'s bound and creates
  nothing; `timeout_ms` is ignored; `configs` are ignored and their names noted on the exchange log with `-v`.
  Every `error_message` is null. Names map through `MessageBus.Names` as `KafkaMetadataAnswerer` maps them.
- `KafkaErrorCode` gains 36, 37, 38, 39 and 42; `KafkaServedApis` and `KafkaApiKey` gain CreateTopics (19).
- Faults: ask `CommandStageFaults.FindAndApplyAsync` (ADR-0141) once per topic, command `CreateTopics`, path the
  Kafka topic name; an `error` rule's code is that topic's `error_code` and the topic is not created. Test with
  `ScriptedFaultPlan`.

## Acceptance criteria

- [x] `KafkaApiVersionsAnswererTests` pins CreateTopics 2-4.
- [x] Named `KafkaCreateTopicsAnswererTests`: a new topic created with an empty log and listed by Metadata; v4 with
      -1 partitions and replication factor creating it; each of 42, 17, 29, 36, 37 (0 and 2 partitions), 38 (0 and 2),
      39 and 44 answered for its topic while the other topics of the request are created.
- [x] A test shows `validate_only` answering 0 and creating nothing, and another a topic with `configs` created and
      the ignored names noted.
- [x] A test with `ScriptedFaultPlan` shows an `error` rule on one topic answered with its code and that topic absent.
- [x] `Kafka.md`'s CreateTopics rows name these tests in place of BL-950.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).
- Built `KafkaCreateTopicsAnswerer` (v2 to v4); `KafkaServedApis`/`KafkaApiKey` gain CreateTopics 19 (2 to 4, first flexible 5); `KafkaErrorCode` gains 36, 37, 38, 39 and 42. 378 Kafka tests pass; the library is 100% line and branch.
- Choice: `KafkaRequest` gains `Transport` (the `IConnection`) because `CommandStageFaults.FindAndApplyAsync` needs the connection a `drop` aborts; the answerer then returns `KafkaAnswer.Close` so nothing more is written. Tests constructing `KafkaRequest` pass an empty `InMemoryConnection`.
- Choice: the fault plan is asked first for each topic (every occurrence, duplicates included), before the checks, as the POP3 and IMAP command faults are asked in place of the command. An `error` rule whose reply is not an integer 1 to 127 (ADR-0124 decision 4's kafka row) - none, 0, 128, text - is noted and not applied, and the topic is answered as usual, as CoAP treats a reply that does not fit; a rule of any other kind is ignored there.
- Choice: ignored `configs` are noted for every topic that carries any, whatever its answer, as `CreateTopics ignored the configs of <name>: <names>.`.
- `Kafka.md` gains `Kafka:Surl-CreateTopics-2` for the fault rule, and loses CreateTopics' "Not served yet" line. `surl` does not compose the answerer yet: that is BL-952.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. The Kafka broker answers CreateTopics v2-v4 on the message bus, with ApiVersions listing it
