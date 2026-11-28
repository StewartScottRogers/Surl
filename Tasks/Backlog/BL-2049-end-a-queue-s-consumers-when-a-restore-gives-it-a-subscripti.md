---
id: BL-2049
title: End a queue's consumers when a restore gives it a subscription description
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2049 — End a queue's consumers when a restore gives it a subscription description

## Goal

A consumer added to a queue with no description receives nothing a restore puts into that queue once the restore
makes it a subscription queue.

## Context

- Found by the code review of BL-2047 (2026-11-27), read in the code, not run.
- A client with the `$queue` grant consumes `surl-subscription-<hash>` while no such queue exists, after declaring it
  (STOMP `SUBSCRIBE`, AMQP 0-9-1). `Restore` of a snapshot holding the described queue then reuses that `NamedQueue`,
  because `EmptyQueuesAndRemoveUnusedOnes` keeps a queue with consumers. `Refill`
  (`MessageBus.SeedResetSnapshot.cs`) sets the description and bindings and dispatches the restored messages to that
  consumer, whose filters were never judged (ADR-0146, Amendment 5).
- A likely rule: a restore that changes a queue's description ends its consumers (and get holders) before refilling.
  Decide in a further ADR-0146 amendment.

## Acceptance criteria

- [ ] `Surl.MessageBus.UnitTests`: a consumer of an undescribed queue, restored over with a snapshot in which that name carries a description, receives none of the restored messages, and they stay ready.
- [ ] `Surl.MessageBus.UnitTests`: a restore that leaves a queue's description unchanged keeps its consumers, as before.
- [ ] ADR-0146 and `Surl.MessageBus.UnitLibrary/CLAUDE.md` state the rule, and their sentence naming this limit is gone.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
