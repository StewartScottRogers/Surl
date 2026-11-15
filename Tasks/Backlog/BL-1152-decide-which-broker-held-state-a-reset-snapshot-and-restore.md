---
id: BL-1152
title: Decide which broker-held state a reset, snapshot and restore of the message bus cover
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1152 — Decide which broker-held state a reset, snapshot and restore of the message bus cover

## Goal

A new ADR, marked "Decided by Claude under Stewart's delegation", says which state each message broker holds outside `MessageBus` and what a reset, snapshot and restore of the bus does to it, so a test that resets between tests gets no message, exchange or session left over from the last one.

## Context

- Stand-in audit 2026-11-14. ADR-0124 decision 3: a bus reset keeps the configured topics, queues, maps and access rules "holding no messages, no retained messages and empty logs"; live subscriptions stay and unacknowledged deliveries are dropped. `MessageBus.Reset` (`Protocols/Messaging/Surl.MessageBus.UnitLibrary/MessageBus.SeedResetSnapshot.cs`) resets only the bus.
- State the audit found outside the bus, untouched by `ComposedStores.ResetAsync`, `TakeSnapshotAsync` and `RestoreAsync`:
  - AMQP 0-9-1: `Amqp0ExchangeTable` (`Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/Amqp0ExchangeTable.cs`) - exchanges and bindings a client declared, persisted through `Amqp0ExchangeFiles` with `--directory`.
  - MQTT 5: `Mqtt5BrokerState` (`Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/Mqtt5BrokerState.cs`) - `sessions` (with each `Mqtt5Session`'s queued deliveries), `pendingWills`, `connectedClients`.
- Not yet audited: the MQTT 3.1.1 server (`Surl.Protocol.Mqtt.UnitLibrary`), AMQP 1.0 durable subscriptions (`Amqp1BusNodeDirectory`, ADR-0146), Kafka's `KafkaGroupCoordinator` and `KafkaProducerStates`, STOMP and NATS. This task reads each and classifies its state.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` (next free number) lists every broker's state held outside `MessageBus` - at least the AMQP 0-9-1 exchange table and the MQTT 5 sessions, wills and queued deliveries, plus what reading MQTT 3.1.1, AMQP 1.0, Kafka, STOMP and NATS finds - each classed as configured state, test data or live session state.
- [ ] For each, it decides what reset, snapshot and restore do (for example: declared exchanges and bindings return to the configured and built-in ones; offline sessions' queued deliveries are dropped) and how the broker learns of it (a registration in `ComposedStores` per BL-1158, or a reaction to the bus's own `Reset` and `Restored` changes), consistent with ADR-0124 decision 3's "live sessions" rules.
- [ ] It decides what a broker with `--directory` does, consistent with ADR-0124's refusal for stores on disk.
- [ ] BL-1170 (AMQP 0-9-1) and BL-1171 (MQTT 5) are named under "Acted on by"; for any other broker it finds state that must follow a reset, this task files one task per broker with `task-board.ps1 new`, exact `touches` and `depends-on` this task and BL-1158, and lists it in the ADR.
- [ ] ADR-0124 gains an "Amended by" line pointing at the new ADR.

## Notes

## Log

- 2026-11-14: Created.
