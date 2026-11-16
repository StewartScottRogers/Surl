---
id: BL-1227
title: Publish StoreChangedEvent for AMQP 0-9-1 exchange and binding changes and honour out-of-space on declare
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1215]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1227 — Publish StoreChangedEvent for AMQP 0-9-1 exchange and binding changes and honour out-of-space on declare


## Goal

A test sees `Created`, `Bound`, `Unbound` and `Deleted` `StoreChangedEvent`s for AMQP 0-9-1 exchange and binding changes, and can make an exchange declare fail with an `out-of-space` rule.

## Context

- Decision: ADR-0166 (decisions 1 to 3). Where: `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary` (`Amqp0BrokerConnection.Exchanges.cs`), plus `StoreChange.Bound` and `Unbound` in `Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreChange.cs` (append only, with any tests or name tables that list members).
- Actions: `exchange.declare` creating, `exchange.delete`, `queue.bind`/`exchange.bind` adding, `queue.unbind`/`exchange.unbind` removing; a deleted exchange publishes `Unbound` per binding then `Deleted`. Nothing for redeclare, repeat bind, default-topic-space binds.
- Fields: `Name` as ADR-0166 decision 2; no binding arguments in the event.
- Faults: ask the plan at `FaultStage.StoreWrite` (`Path` = exchange name) before a creating declare and an adding bind; a match answers connection close 506 `RESOURCE_ERROR` and writes nothing.

## Acceptance criteria

- [x] Tests with a recording publisher show each event above with its `Change`, `Name` and `ExchangeId`, and show no event for a redeclare, a repeated bind and a default-topic-space bind.
- [x] A test shows a deleted exchange publishes its `Unbound` events before its `Deleted`.
- [x] A test shows an `out-of-space` rule refuses a creating declare with 506, leaves the table unchanged, and does not refuse a redeclare.
- [x] Build clean, fast tests green, 100% line and branch coverage of the changed code.

## Notes

- The out-of-space ask runs in the async frame step (RefuseWhenOutOfSpaceAsync) before the synchronous handler, because handlers are sync; it pre-parses the declare or bind and asks only when the table would grow. An unknown queue, bad x-match, invalid name or topic-space bind is answered as usual.
- Only queue.bind exists in the broker (xchange.bind is 540), and the table has no auto-delete exchanges, so neither publishes. queue.delete removing a queue's bindings publishes no Unbound (the bus publishes the queue delete); not in ADR-0166's list.
- Amqp0ExchangeDeclareOutcome.Created added so a redeclare is told from a create.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Exchange store changes published; out-of-space honoured on declare and bind
