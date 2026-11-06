---
id: BL-801
title: Seed, reset, snapshot and restore the message bus in Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-790]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-801 — Seed, reset, snapshot and restore the message bus in Surl.MessageBus

## Goal

A test can publish a message (retained or not) or enqueue one on a named queue before its client connects, return
the bus to its configured state between tests, and take and restore an in-memory snapshot of retained messages,
queues and topic logs, each through `MessageBus`, each published on the event stream.

## Context

- Decision: BL-822's ADR decision 3 (reset target - planning default: configured topics, queues, maps and access
  rules stay, holding no messages, no retained messages and empty logs; what happens to live subscriptions and
  unacknowledged deliveries on reset and restore; snapshot bound; persisted bus under `<path>/.surl/bus`).
- Where: `Protocols/Messaging/Surl.MessageBus.UnitLibrary` (`MessageBus.cs`, `MessageBus.Queues.cs`,
  `MessageBus.Logs.cs`, `MessageBus.Persistence.cs`). Seeding uses the same publish path the brokers use, by
  canonical topic name (ADR-0100 decision 3.2), so live subscribers on any protocol receive a seeded message.
- Each operation publishes the `StoreChangedEvent`s BL-790 defined (or a summary event, as the ADR says).
- Stand-in: store = the message bus; controllable. Composition exposes it in BL-805; `Surl.Api` in BL-810.

## Acceptance criteria

- [x] Named `MessageBusTests`: a seeded retained message is delivered to a later subscriber; a seeded message
      reaches a live subscription and a named queue's consumer; reset leaves configured topics and queues empty and
      does to live subscriptions what the ADR says; snapshot, change, restore gives back the same retained
      messages, queue contents and log offsets; each publishes its events.
- [x] A named test shows the persisted-bus behaviour the ADR decides.
- [x] `dotnet build Protocols/Messaging/Surl.MessageBus.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and the library keeps 100% line and branch coverage.

## Notes

- Tests live in `MessageBusSeedResetSnapshotTests` (one class for the four operations, beside the other
  `MessageBus*Tests` classes) rather than in a class named `MessageBusTests`.
- A seed bypasses the topic access grants as well as the fault plan: it carries no account (a test
  fixture, not a client), so an access grant has nobody to judge. Size, name and queue bounds still apply,
  as ADR-0124 decision 3 says "quotas, names and events apply".
- Reset and restore publish one `StoreChangedEvent` named `#` (`MessageBus.WholeBusChangeName`), the
  canonical filter every topic matches, standing for the whole bus.
- Reset keeps a connection-declared queue (emptied) while it is exclusive to a connection or has a
  consumer, so a live consumer keeps receiving; any other runtime queue goes. Unacknowledged deliveries are
  dropped, so a later settle answers `UnknownDeliveryTag` (ADR-0124 "live sessions").
- Snapshot leaves out queues exclusive to one connection (they die with it); restore re-marks a message
  that was unacknowledged at snapshot time as redelivered. `TakeSnapshot(maxBytes)` defaults to 256 MiB
  and refuses, never truncates.
- A bus loaded from a data directory (`<path>/.surl/bus`) refuses reset, snapshot and restore with
  `InvalidOperationException`; seeding it is ordinary writing and saves
  (`APersistedBus_RefusesResetSnapshotAndRestore_ButIsSeededAndSaved`).
- Coverage of `Surl.MessageBus.UnitLibrary` measured at 100% line and 100% branch.

- Tests live in `MessageBusSeedResetSnapshotTests` (one class for the four operations, beside the other
  `MessageBus*Tests` classes) rather than in a class named `MessageBusTests`.
- A seed bypasses the topic access grants as well as the fault plan: it carries no account (a test
  fixture, not a client), so an access grant has nobody to judge. Size, name and queue bounds still apply,
  as ADR-0124 decision 3 says "quotas, names and events apply".
- Reset and restore publish one `StoreChangedEvent` named `#` (`MessageBus.WholeBusChangeName`), the
  canonical filter every topic matches, standing for the whole bus.
- Reset keeps a connection-declared queue (emptied) while it is exclusive to a connection or has a
  consumer, so a live consumer keeps receiving; any other runtime queue goes. Unacknowledged deliveries are
  dropped, so a later settle answers `UnknownDeliveryTag` (ADR-0124 "live sessions").
- Snapshot leaves out queues exclusive to one connection (they die with it); restore re-marks a message
  that was unacknowledged at snapshot time as redelivered. `TakeSnapshot(maxBytes)` defaults to 256 MiB
  and refuses, never truncates.
- A bus loaded from a data directory (`<path>/.surl/bus`) refuses reset, snapshot and restore with
  `InvalidOperationException`; seeding it is ordinary writing and saves
  (`APersistedBus_RefusesResetSnapshotAndRestore_ButIsSeededAndSaved`).
- Coverage of `Surl.MessageBus.UnitLibrary` measured at 100% line and 100% branch.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. MessageBus seeds by publish or enqueue, resets to its configured topics and queues, and snapshots and restores retained messages, queues and logs, each published on the event stream
