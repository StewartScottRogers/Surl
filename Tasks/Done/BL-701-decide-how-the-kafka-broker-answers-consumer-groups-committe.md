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
completed: 2026-11-06
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

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by Claude
      under Stewart's delegation", answers each API above citing the guide, and is listed in the folder's
      `README.md`.
- [x] `Documentation/Conformance/Extended/Kafka.md`'s "Not required of this broker" rows for groups, CreateTopics
      and InitProducerId are replaced by rows naming the tasks filed to build them.

## Notes

- Filed by BL-516 (ADR-0105 decision 1).
- Wrote ADR-0145 (ADR-0143 is taken on another lane and ADR-0137 is a gap another lane holds). Read from the Kafka 4.4 protocol guide, 2026-11-06. Classic group protocol only, non-flexible ranges; node 0 coordinates; committed offsets kept in Surl.MessageBus so reset/snapshot/restore/persistence carry them (amends ADR-0100 3.3); groups as `$group/<id>` in `--topic-access` (amends ADR-0102 3.4); CreateTopics on the bus; InitProducerId with seedable ids and Produce sequence checking (amends ADR-0105 4 and 7, ADR-0124 1). The amendments are recorded in ADR-0145 only, because this task touches only that folder and Kafka.md; the earlier ADRs point forward when their builders edit them.
- The leader picks the first protocol every member lists instead of a vote: same answer when members agree, and deterministic on a tie.
- Filed BL-929 to BL-935 (event contracts, bus offsets and group access, coordinator membership, offsets and describe/list, CreateTopics, InitProducerId with sequences, composition).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0145 decides the Kafka group coordinator, committed offsets, CreateTopics and InitProducerId; BL-929 to BL-935 build them
