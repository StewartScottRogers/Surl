---
id: BL-754
title: Append a Kafka Produce partition to the bus all or nothing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-519]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-754 — Append a Kafka Produce partition to the bus all or nothing

## Goal

A Produce partition whose records meet a bus store bound part of the way through (`StoreFull`,
`TooManyTopics`) appends none of them, as ADR-0105 decision 4 ("a Produce is all or nothing per
partition") and requirement `Kafka:Surl-Produce-1` say.

## Context

- BL-519's `KafkaProduceAnswerer.Publish` publishes a partition's records one `MessageBus.Publish` at a
  time. Every check the broker can make first (batch checks, `MaxMessageBytes`, topic access, creating
  the log) is made before the first publish, but `MaxLogBytes` (`StoreFull`) can only be judged by the
  bus, so a partition can be answered `POLICY_VIOLATION` (44) with its first records already appended.
- The fix: a bus method publishing a list of messages on one topic under one lock, planned in full
  (`PlanLogAppend` for the whole list, retention included) before any is appended, refused whole or
  taken whole, returning each offset; then `KafkaProduceAnswerer.Publish` uses it.
- ADR-0100 decisions 3.3 and 3.6; ADR-0105 decisions 4 and 7.

## Acceptance criteria

- [ ] A `Surl.MessageBus.UnitTests` test shows a list of messages that would pass `MaxLogBytes` on
      its last message is refused whole: the log's high watermark is unchanged and no subscriber
      receives any of them.
- [ ] A `KafkaProduceAnswererTests` test shows a batch of three records whose third passes the bus's
      `MaxLogBytes` is answered 44 and the topic's log stays empty.
- [ ] `Kafka.md`'s `Kafka:Surl-Produce-1` row names the new test and drops the BL-754 note.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
