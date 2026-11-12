---
id: BL-949
title: Answer Kafka OffsetCommit, OffsetFetch, DescribeGroups and ListGroups
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-947, BL-948]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-949 — Answer Kafka OffsetCommit, OffsetFetch, DescribeGroups and ListGroups

## Goal

The Kafka broker commits and fetches consumer group offsets in `Surl.MessageBus` and describes and lists its groups:
OffsetCommit v2-v7, OffsetFetch v1-v5, DescribeGroups v0-v4 and ListGroups v0-v2 are answered and listed by
ApiVersions.

## Context

- Decision: [ADR-0145](../../Documentation/Planning/Decisions/ADR-0145-how-the-kafka-broker-answers-consumer-groups-committed-offsets-createtopics-and-initproducerid.md) decisions 1, 4, 5, 6 and 10. Specification: the Apache Kafka 4.4 protocol guide
  (<https://kafka.apache.org/44/design/protocol>); unit tests replay bytes written by hand from it (ADR-0083).
- Offsets live in the bus (BL-947); Kafka names map to canonical topics through `MessageBus.Names` for `kafka`, as
  `KafkaMetadataAnswerer` maps them. Membership checks ask BL-948's `KafkaGroupCoordinator`; a member's commit
  refreshes its session. A simple commit (generation -1, empty member id) creates an `Empty` group with an empty
  `protocol_type` when it has no members, else 25.
- OffsetCommit per partition, in order: 17, 3 (partition other than 0, or no log), 29, 12 (metadata over 4096 UTF-8
  bytes), 44 (the bus's `StoreFull` or an out-of-space rule), else 0. `retention_time_ms` read and ignored.
- OffsetFetch: group access 30 top-level (v2-v5) or per partition (v1); null `topics` (v2 on) lists every committed
  topic in ordinal order of Kafka name; none committed is -1, leader epoch -1, metadata empty, error 0.
- DescribeGroups: `Dead` for a group that does not exist, 30 for a denied one, members in join order with
  `client_host` `/<address>`, `authorized_operations` `-2147483648` unless asked, then READ and DESCRIBE bits.
  ListGroups: every group with a member or a committed offset the caller may use, ordinal order.
- Faults: each API asks `CommandStageFaults.FindAndApplyAsync` (ADR-0141), command the API name, path the group id
  (none for ListGroups); an `error` rule's code goes where decision 10 says. Test with `ScriptedFaultPlan`.

## Acceptance criteria

- [ ] `KafkaApiVersionsAnswererTests` pins OffsetCommit 2-7, OffsetFetch 1-5, DescribeGroups 0-4 and ListGroups 0-2.
- [ ] Named OffsetCommit tests: a member's commit stored and fetched back; a simple commit into an empty group
      accepted and into a group with members 25; 22 for another generation; 27 while completing a rebalance; each
      per-partition error 17, 3, 29, 12 and 44; one `StoreChange.Committed` event per stored partition.
- [ ] Named OffsetFetch tests: v1 named topics, v2 null topics listing every committed topic, -1 for none, v5's
      leader epoch, a denied group 30 top-level on v2 and per partition on v1.
- [ ] Named DescribeGroups and ListGroups tests: a `Stable` group described with its members, metadata and
      assignments; `Dead` for an unknown group; 30 for a denied one; ListGroups in ordinal order including a group
      known only by its offsets and leaving out a denied one.
- [ ] A test with `ScriptedFaultPlan` shows an `error` rule on `OffsetCommit` answered in every partition with
      nothing stored.
- [ ] `Kafka.md`'s committed offsets and group listing rows name these tests in place of BL-949.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. returned by the shift starting 20261111-212123: it was in Doing but no stopped lane held it; look for its work in 'git stash list' and in factory/BL-949-* branches before redoing it
