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
completed:
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

- [ ] `KafkaApiVersionsAnswererTests` pins CreateTopics 2-4.
- [ ] Named `KafkaCreateTopicsAnswererTests`: a new topic created with an empty log and listed by Metadata; v4 with
      -1 partitions and replication factor creating it; each of 42, 17, 29, 36, 37 (0 and 2 partitions), 38 (0 and 2),
      39 and 44 answered for its topic while the other topics of the request are created.
- [ ] A test shows `validate_only` answering 0 and creating nothing, and another a topic with `configs` created and
      the ignored names noted.
- [ ] A test with `ScriptedFaultPlan` shows an `error` rule on one topic answered with its code and that topic absent.
- [ ] `Kafka.md`'s CreateTopics rows name these tests in place of BL-950.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).

## Log

- 2026-11-06: Created.
