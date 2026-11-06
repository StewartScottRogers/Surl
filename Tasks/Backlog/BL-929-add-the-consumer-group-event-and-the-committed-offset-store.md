---
id: BL-929
title: Add the consumer group event and the committed-offset store change to the event contracts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-929 — Add the consumer group event and the committed-offset store change to the event contracts

## Goal

`Surl.Protocol.Abstractions` has `ConsumerGroupChangedEvent`, `ConsumerGroupChange` and `StoreChange.Committed`,
and the control endpoint writes and filters the new event kind as `consumer_group_changed`, so the Kafka group
coordinator (BL-931) and the bus's committed offsets (BL-930) have contracts to publish on.

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

- [ ] `ConsumerGroupChangedEvent`, `ConsumerGroupChange` and `StoreChange.Committed` exist in
      `Foundation/Surl.Protocol.Abstractions.UnitLibrary` with XML doc comments citing ADR-0145 decision 10, and an
      Abstractions test pins `ConsumerGroupChange`'s six members in that order.
- [ ] A `ControlEventKindsTests` test shows `consumer_group_changed` in `Known`, the event's exchange id and account
      read back, and a `ControlEventWriterTests` test pins the JSON written for one event of each change.
- [ ] A `ControlEventWriterTests` test pins a `StoreChangedEvent` with `Committed` written as `"change":"committed"`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).

## Log

- 2026-11-06: Created.
