---
id: BL-946
title: Add the consumer group event and the committed-offset store change to the event contracts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-946 — Add the consumer group event and the committed-offset store change to the event contracts

## Goal

`Surl.Protocol.Abstractions` has `ConsumerGroupChangedEvent`, `ConsumerGroupChange` and `StoreChange.Committed`,
and the control endpoint writes and filters the new event kind as `consumer_group_changed`, so the Kafka group
coordinator (BL-948) and the bus's committed offsets (BL-947) have contracts to publish on.

## Context

- Decision: [ADR-0145](../../Documentation/Planning/Decisions/ADR-0145-how-the-kafka-broker-answers-consumer-groups-committed-offsets-createtopics-and-initproducerid.md) decision 10, amending ADR-0124 decision 1's contract.
- `ConsumerGroupChangedEvent(long? ExchangeId, string Scheme, string Group, ConsumerGroupChange Change, string?
  MemberId, int GenerationId, string? Account) : SurlEvent`; `ConsumerGroupChange` is `MemberJoined`, `MemberLeft`,
  `MemberRemoved`, `MemberFenced`, `GenerationStarted`, `AssignmentDistributed`, each with an XML doc comment saying
  when ADR-0145 decision 3 publishes it. `StoreChange.Committed`: "An offset was committed for a consumer group",
  named `$group/<group id, % and / percent-encoded>/<canonical topic>`.
- `Application/Surl.Control.UnitLibrary`: `ControlEventKinds.Known`, `ExchangeIdOf`, the account the event names
  (its `Account`) and `ControlEventWriter`'s members in snake_case (ADR-0136), as the existing kinds are written.
- Contracts first: this task touches Abstractions, so it runs apart from every protocol task (task-board skill).

## Acceptance criteria

- [x] `ConsumerGroupChangedEvent`, `ConsumerGroupChange` and `StoreChange.Committed` exist in
      `Foundation/Surl.Protocol.Abstractions.UnitLibrary` with XML doc comments citing ADR-0145 decision 10, and an
      Abstractions test pins `ConsumerGroupChange`'s six members in that order.
- [x] A `ControlEventKindsTests` test shows `consumer_group_changed` in `Known`, the event's exchange id and account
      read back, and a `ControlEventWriterTests` test pins the JSON written for one event of each change.
- [x] A `ControlEventWriterTests` test pins a `StoreChangedEvent` with `Committed` written as `"change":"committed"`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).
- Delivered directly (a contract and its writer, about 60 lines): the pipeline's plan is ADR-0145 decision 10 itself, so no separate protocol-architect plan was needed.
- Choice: `ControlEventJson` gains `group`, `member_id` and `generation_id` at its end, so no existing event's JSON changes (null members are left out). A consumer group event writes `exchange_id`, `scheme`, `account`, `change`, `group`, `member_id`, `generation_id`, reusing `change` for the `ConsumerGroupChange` as `store_changed` does for `StoreChange`.
- `ControlEventKinds.AccountOf` returns the event's `Account`, so `?account=` matches group changes.
- The `kind=nope` refusal's list in `ControlEventRequestsTests` now begins with `consumer_group_changed`.
- Coverage: Surl.Control.UnitLibrary and Surl.Protocol.Abstractions.UnitLibrary 100% line and branch (Code Coverage collector, cobertura).
- Follow-up filed: BL-968, the same kind in Surl.Mcp's `get_events` (outside this task's `touches`).

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ConsumerGroupChangedEvent, ConsumerGroupChange and StoreChange.Committed exist; the control endpoint writes and filters consumer_group_changed
