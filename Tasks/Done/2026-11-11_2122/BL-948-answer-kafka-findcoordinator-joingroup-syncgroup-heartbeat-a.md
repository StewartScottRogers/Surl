---
id: BL-948
title: Answer Kafka FindCoordinator, JoinGroup, SyncGroup, Heartbeat and LeaveGroup with a group coordinator
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-946, BL-947, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-06
completed: 2026-11-11
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

- [x] `KafkaApiVersionsAnswererTests` pins the ranges FindCoordinator 0-2, JoinGroup 0-5, SyncGroup, Heartbeat and
      LeaveGroup 0-3, and still no ConsumerGroupHeartbeat (68).
- [x] Named FindCoordinator tests: group key answered node 0 with the advertised host and port; `key_type` 1 is 42
      with node -1; a denied group is 30.
- [x] Named JoinGroup tests: each check of decision 3 in order (24, 30, 26, 23, 25, 82, 81, 44, 23); v4 empty member id
      answered 79 then joined; v3 joined at once; two members answered with generation 1, the earlier member as leader
      listing both, the follower with no members; the leader's first common protocol chosen; a static rejoin fencing
      the old id.
- [x] Named SyncGroup tests: the follower held until the leader's SyncGroup, each member receiving exactly its bytes
      and an unnamed member empty bytes; 25, 22, 82 and 27 each answered.
- [x] Named Heartbeat and LeaveGroup tests: 27 while preparing a rebalance, 0 in `CompletingRebalance` and `Stable`;
      LeaveGroup v0 and v3 (by member id and by instance id) with per-member errors.
- [x] A session-expiry test advances a fake `TimeProvider` past `session_timeout_ms` and shows the member removed,
      the group rebalanced and a `MemberRemoved` event published; a rebalance-timeout test shows a member that did
      not rejoin removed when the join phase ends.
- [x] A test shows the idle clock paused while a JoinGroup is held and restarted when it is answered.
- [x] A test with `ScriptedFaultPlan` shows an `error` rule on `Heartbeat` answered with its code and nothing changed.
- [x] `Kafka.md`'s consumer group rows name these tests in place of BL-948.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).
- Delivered: `KafkaGroupCoordinator` (membership, join phase, leader, protocol, assignments, sessions, pending ids, bounds, events) and one answerer per API - `KafkaFindCoordinatorAnswerer`, `KafkaJoinGroupAnswerer`, `KafkaSyncGroupAnswerer`, `KafkaHeartbeatAnswerer`, `KafkaLeaveGroupAnswerer` - with `KafkaCommandFault` (the fault rule step they share) and `KafkaHeldAnswer` (idle clock paused while held). `KafkaServedApis`, `KafkaApiKey` and `KafkaErrorCode` gained the rows. Composition in `surl` stays BL-952's.
- Criterion 3 says two members are answered with generation 1. ADR-0145 decision 3 has no initial rebalance delay, so the first member's JoinGroup ends generation 1 alone at once, and a second member is first answered in generation 2. `ServeAsync_TwoMembers_AreAnsweredOneGenerationWithTheEarlierMemberLeaderListingBoth` pins that: both answered the same generation (2), the earlier member leader listing both, the follower no members. The ADR wins over the criterion's number.
- Choices taken (defaults within ADR-0145): a request naming a `group_instance_id` another member id holds is 82 before the unknown-member check (25), so a fenced id is always 82 as decision 3's static-membership bullet says. A refused JoinGroup echoes the member id it was asked with (79 carries the new one). SyncGroup, Heartbeat and LeaveGroup with an empty group id are 25 (the group does not exist), since the ADR names 24 for JoinGroup only. A JoinGroup held when its member leaves is answered 25, or 82 when it was fenced. Events go to the publisher of the group's latest request; a timer's events carry no exchange id, and a removal names the removed member's account.
- Members do not yet keep client id or client host: nothing reads them until DescribeGroups (BL-949), which adds them where it lists them. A group with no members and none pending is forgotten; whether it still has committed offsets (and so reads Empty, not Dead) is DescribeGroups' and ListGroups' question (BL-949).
- `KafkaBrokerTests.ServeAsync_UnknownApiKey_ClosesWithNoBytes` now uses API key 68 (ConsumerGroupHeartbeat, never served) instead of 11, and `KafkaApiVersionRangeTests.Find_UnservedApiKey_IsNull` uses 68; answerer null-argument tests find their range by API key, not by index.
- Coverage: Kafka library 100% line and 100% branch (Measure-CodeQuality.ps1 over the Kafka tests); `CA1502` clean.
- Machine note: SDK 10.0.401 (global.json) is no longer installed on this machine (only 10.0.100-rc.1 and 11.0.100). This lane built and tested with a local, uncommitted `rollForward: latestMajor`, as other lanes do, and reverted it before committing.

- 2026-11-11 (lane 4): resumed by cherry-picking eee15681 and d504712e from factory/BL-948-lane-5-20261111-060243 (task-file conflict resolved by keeping this Doing copy). SDK 10.0.404 is now installed, so the 10.0.401 pin rolls forward with no local change; `dotnet build -warnaserror` clean, all fast tests green (Kafka 503), Kafka library 100% line and branch, 0 failing members.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 5 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-948-lane-5-20261111-060243; start with git cherry-pick --no-commit factory/BL-948-lane-5-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so the cherry-picked lane-5 work cannot be built or tested
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. The Kafka broker serves the classic consumer group protocol: FindCoordinator v0-2, JoinGroup v0-5, SyncGroup, Heartbeat and LeaveGroup v0-3 through KafkaGroupCoordinator, listed by ApiVersions
