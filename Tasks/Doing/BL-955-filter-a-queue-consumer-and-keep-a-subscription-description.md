---
id: BL-955
title: Filter a queue consumer and keep a subscription description on a named queue in Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-955 — Filter a queue consumer and keep a subscription description on a named queue in Surl.MessageBus

## Goal

A queue consumer can be given a predicate so it is offered only the messages it accepts, and a named queue can
carry a subscription description that is persisted, reset, snapshotted and restored with it, so the AMQP 1.0
container can serve selectors on queues and durable subscriptions.

## Context

- ADR-0146 decision 2.4 (filters on a queue link run as a consumer predicate: a consumer is offered only what
  its predicate accepts, a message no consumer accepts stays ready, round robin among those that accept it, the
  predicate called under the bus's lock) and decision 3.2 (a durable subscription's queue keeps its source's
  address and canonical filter encoding beside it, readable without consuming).
- `BusConnection.Consume` (competing consumers, `maxUnacknowledged`), `BusConnection.DeclareQueue` and
  `BusQueueFlags`, `BusConnection.InspectQueue`; persistence under `.surl/bus` (ADR-0100 decision 3.7) and
  `MessageBus.SeedResetSnapshot.cs` (ADR-0124).
- The description is opaque text to the bus. Declaring an existing queue with another description is a new
  `MessageBusOutcome` member (e.g. `QueueDescriptionDiffers`), so the container can tell a changed subscription.

## Acceptance criteria

- [ ] `Consume` takes an optional predicate; a test with two consumers, one accepting only even-numbered
      messages, shows each offered only what it accepts and a message neither accepts staying ready in order.
- [ ] Without a predicate every existing consume test passes unchanged.
- [ ] `DeclareQueue` takes an optional subscription description; `InspectQueue` returns it; declaring again with
      another description answers the new outcome and changes nothing.
- [ ] A test shows the description survives a save and load under `--directory`, and a snapshot and restore,
      and goes with the queue on reset.
- [ ] `Surl.MessageBus.UnitLibrary/CLAUDE.md` describes both; 100% line and branch coverage holds.

## Notes

- Filed by BL-707 (ADR-0146 decisions 2.4 and 3.2).

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
