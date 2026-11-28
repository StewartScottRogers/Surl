---
id: BL-2038
title: Keep an excluded publisher's messages out of a message bus subscription queue as ADR-0146 decides no-local
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2037]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2038 — Keep an excluded publisher's messages out of a message bus subscription queue as ADR-0146 decides no-local

## Goal

`Surl.MessageBus` offers what BL-2037's amendment to ADR-0146 names, so a subscription queue's consumer never
receives a publish the no-local filter withholds from it.

## Context

- Found while working BL-1990 (2026-11-27): a live subscription takes `noLocal`
  (`BusConnection.Subscribe`; `BusSubscription.KeepsFrom`), a queue has no equivalent.
- Read BL-2037's dated amendment to ADR-0146 first: it says whose publishes are withheld, where, and what is
  persisted and snapshotted. Do exactly that.
- Start at `MessageBus.Accept` and `CopyIntoBoundQueues` (`MessageBus.cs`, `MessageBus.Queues.cs`), the batch
  publish (`MessageBus.Batch.cs`), `MessageBus.SubscriptionQueues.cs`, and the persistence and snapshot files.
- Stand-in: the shared message bus. State the amendment adds to a queue is reset, snapshotted and restored with
  it; a withheld copy publishes no store change on the event stream. No new fault.
- BL-2039 uses the operation from the AMQP 1.0 container.

## Acceptance criteria

- [ ] `Surl.MessageBus.UnitTests`: a publish the amendment says is withheld is never delivered from the subscription queue, through both a single publish and a batch publish, and any other publish is delivered.
- [ ] `Surl.MessageBus.UnitTests`: a queue not using the operation receives its own connection's publishes as before.
- [ ] `Surl.MessageBus.UnitTests`: what the amendment says is persisted survives a save and load and a snapshot and restore, and a bus saved without it still loads.
- [ ] `Surl.MessageBus.UnitLibrary/CLAUDE.md` states the rule.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
