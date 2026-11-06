---
id: BL-948
title: Answer Kafka FindCoordinator, JoinGroup, SyncGroup, Heartbeat and LeaveGroup with a group coordinator
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-946, BL-947]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-948 — Answer Kafka FindCoordinator, JoinGroup, SyncGroup, Heartbeat and LeaveGroup with a group coordinator

## Goal

The Kafka broker serves the classic consumer group protocol: a `KafkaGroupCoordinator` runs each group's
membership, rebalances and session timeouts, and FindCoordinator v0-v2, JoinGroup v0-v5, SyncGroup v0-v3,
Heartbeat v0-v3 and LeaveGroup v0-v3 are answered and listed by ApiVersions.

## Context

- Decision: [ADR-0145](../../Documentation/Planning/Decisions/ADR-0145-how-the-kafka-broker-answers-consumer-groups-committed-offsets-createtopics-and-initproducerid.md) decisions 1, 2, 3, 6, 9 and 10. Specification: the Apache Kafka 4.4 protocol
  guide (<https://kafka.apache.org/44/design/protocol>), each API's schema and the "Error Codes" table; unit tests
  replay bytes written by hand from it (ADR-0083).
- `KafkaGroupCoordinator` (new, in the Kafka library): groups, members (id, instance id, client id, client host,
  protocols, session and rebalance timeouts, assignment), generation, state, leader, chosen protocol; timers from a
  constructor-injected `TimeProvider`; bounds `DefaultMaxGroups` 1000 (44), `DefaultMaxMembersPerGroup` 1000 (81),
  `DefaultMaxGroupStateBytes` 268435456 (44). One `IKafkaApiAnswerer` per API, each taking the coordinator;
  `KafkaServedApis` and `KafkaApiKey` gain the five rows; `KafkaErrorCode` gains 22 to 27, 30, 42, 79, 81 and 82.
- FindCoordinator: node 0 at Metadata's advertised address (`KafkaMetadataAnswerer`'s rule); `key_type` other
  than 0 is 42. Group access through BL-947's bus check (30), before anything else about the group.
- Member ids `<client_id or group_instance_id>-<uuid>` from `ExchangeContext.Identifiers`; `MEMBER_ID_REQUIRED` on
  v4-v5; join phase without initial delay; leader and protocol as decision 3 says; assignments relayed verbatim;
  static members fenced (82); sessions refreshed by JoinGroup, SyncGroup and Heartbeat (OffsetCommit is BL-949's).
- A held JoinGroup or SyncGroup pauses `ExchangeContext.IdleClock` and restarts it once answered (decision 9).
- Events: one `ConsumerGroupChangedEvent` per change on `ExchangeContext.Events` (BL-946), `MemberRemoved` with no
  exchange id.
- Faults: each API asks `CommandStageFaults.FindAndApplyAsync` (ADR-0141) after the login, command the API name,
  path the group id (FindCoordinator its key); an `error` rule's code goes in the top-level `error_code`. `Surl.Cli`'s
  acceptance of `kafka` error rules is BL-834's; test with `ScriptedFaultPlan`.

## Acceptance criteria

- [ ] `KafkaApiVersionsAnswererTests` pins the ranges FindCoordinator 0-2, JoinGroup 0-5, SyncGroup, Heartbeat and
      LeaveGroup 0-3, and still no ConsumerGroupHeartbeat (68).
- [ ] Named FindCoordinator tests: group key answered node 0 with the advertised host and port; `key_type` 1 is 42
      with node -1; a denied group is 30.
- [ ] Named JoinGroup tests: each check of decision 3 in order (24, 30, 26, 23, 25, 82, 81, 44, 23); v4 empty member id
      answered 79 then joined; v3 joined at once; two members answered with generation 1, the earlier member as leader
      listing both, the follower with no members; the leader's first common protocol chosen; a static rejoin fencing
      the old id.
- [ ] Named SyncGroup tests: the follower held until the leader's SyncGroup, each member receiving exactly its bytes
      and an unnamed member empty bytes; 25, 22, 82 and 27 each answered.
- [ ] Named Heartbeat and LeaveGroup tests: 27 while preparing a rebalance, 0 in `CompletingRebalance` and `Stable`;
      LeaveGroup v0 and v3 (by member id and by instance id) with per-member errors.
- [ ] A session-expiry test advances a fake `TimeProvider` past `session_timeout_ms` and shows the member removed,
      the group rebalanced and a `MemberRemoved` event published; a rebalance-timeout test shows a member that did
      not rejoin removed when the join phase ends.
- [ ] A test shows the idle clock paused while a JoinGroup is held and restarted when it is answered.
- [ ] A test with `ScriptedFaultPlan` shows an `error` rule on `Heartbeat` answered with its code and nothing changed.
- [ ] `Kafka.md`'s consumer group rows name these tests in place of BL-948.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).

## Log

- 2026-11-06: Created.
