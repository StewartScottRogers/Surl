---
id: BL-1215
title: Decide which client changes to the AMQP 0-9-1 exchange table, the MQTT 5 sessions and the Kafka producer states publish StoreChangedEvent
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-060
created: 2026-11-15
completed:
---
# BL-1215 — Decide which client changes to the AMQP 0-9-1 exchange table, the MQTT 5 sessions and the Kafka producer states publish StoreChangedEvent

## Goal

An ADR, decided by Claude under Stewart's delegation, says which changes a client makes to the broker-held stores ADR-0159 registered publish `StoreChangedEvent`, with which `Change` and `Name`. The follow-up tasks are filed.

## Context

- Found by the stand-in audit of 2026-11-15. ADR-0159 registered three broker-held stores with `ComposedStores` as `StoreKind.Exchanges` (AMQP 0-9-1), `StoreKind.Sessions` (MQTT 5) and `StoreKind.Producers` (Kafka). Their registrations (`Application/Surl.Composition.UnitLibrary/Amqp0ExchangeStoreRegistration.cs`, `Mqtt5SessionStoreRegistration.cs`, `KafkaBrokerStoreRegistration.cs`) publish only `Reset` and `Restored`.
- What a client changes is never published. The AMQP 0-9-1 broker publishes no event for `exchange.declare`, `exchange.delete`, `queue.bind` or `queue.unbind`: the only `Publish` in `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary` is a `FaultAppliedEvent`. ADR-0159 classes declared exchanges and bindings as "test data", and ADR-0124's "Observable" principle publishes every store change. MQTT 5 sessions and Kafka producer states are classed "live session state" and "test data tied to the log".
- Questions:
  1. For each of the three, which client actions are store changes and which are live session state that publishes nothing?
  2. The `StoreChange` member and `Name` for each, e.g. `Written` with the exchange name for a declare, `Deleted` for a delete, and what a binding's `Name` is. Does `StoreChange` need a new member? The enum only grows (configuration as data).
  3. Does `out-of-space` at `FaultStage.StoreWrite` apply to an exchange declare? ADR-0158 decision 7 set the precedent for when a store gains a client write.
- Standing rules: no secret in an event; events never block (ADR-0124 decision 1).

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/`, with the next free number, marked "Decided by Claude under Stewart's delegation", answers questions 1 to 3 for AMQP 0-9-1, MQTT 5 and Kafka, and amends ADR-0159 (header "Amended by").
- [ ] One feature task per broker library that must publish is filed with `task-board.ps1 new`, depending on this task. Each names the actions, the event fields and a test with a recording publisher. Each is listed in Notes, or Notes says why none is needed.

## Notes

## Log

- 2026-11-15: Created.
