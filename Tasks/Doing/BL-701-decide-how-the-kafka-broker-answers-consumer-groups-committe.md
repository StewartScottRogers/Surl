---
id: BL-701
title: Decide how the Kafka broker answers consumer groups, committed offsets, CreateTopics and InitProducerId
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-516]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-701 — Decide how the Kafka broker answers consumer groups, committed offsets, CreateTopics and InitProducerId

## Goal

An ADR decides how the Kafka broker serves the APIs ADR-0105 decision 1 left "not served yet" - the consumer group
coordinator (FindCoordinator, JoinGroup, SyncGroup, Heartbeat, LeaveGroup, OffsetCommit, OffsetFetch, and
DescribeGroups and ListGroups if served), CreateTopics and InitProducerId - and files the tasks that build them.

## Context

- ADR-0105 (the broker, its version ranges, one partition a topic, no transactions); ADR-0100 decision 3.3
  ("Kafka's consumer groups and committed offsets stay inside the Kafka broker") and 3.2 (CreateTopics with more
  than one partition is `INVALID_PARTITIONS` (37)); ADR-0102 decision 3.4 (topic access) and ADR-0032.
- Specification: the Apache Kafka 4.4 protocol guide (<https://kafka.apache.org/44/design/protocol>), each API's
  schemas and the error codes table; the classic group protocol and, if served, the KIP-848 consumer protocol
  (ConsumerGroupHeartbeat). Pin the documentation version read, as ADR-0105 does.
- Decide: version ranges (non-flexible first, as ADR-0105); the group coordinator being node 0; rebalance and
  session timeouts on the injected `TimeProvider`; the one assignor protocol a group may use and how the leader's
  assignment is relayed; where committed offsets are kept (in memory, or under `.surl/` and how, given BL-634's
  bus persistence); group-level access (ADR-0102 has topics only: `GROUP_AUTHORIZATION_FAILED` or none); bounds on
  groups, members and committed offsets (ADR-0006); CreateTopics' answers against the bus; InitProducerId for an
  idempotent producer (a producer id and epoch, sequence checking or not) without transactions.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by Claude
      under Stewart's delegation", answers each API above citing the guide, and is listed in the folder's
      `README.md`.
- [ ] `Documentation/Conformance/Extended/Kafka.md`'s "Not required of this broker" rows for groups, CreateTopics
      and InitProducerId are replaced by rows naming the tasks filed to build them.

## Notes

- Filed by BL-516 (ADR-0105 decision 1).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
