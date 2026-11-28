---
id: BL-2095
title: Give a queue a restore reuses the snapshot's owner and flags
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2095 — Give a queue a restore reuses the snapshot's owner and flags

## Goal

A queue a restore puts back has the snapshot's flags and no owning connection, whatever live queue held its name.

## Context

- Found by the code review of BL-2051 (2026-11-28), read in the code, not run.
- `Refill` (`MessageBus.SeedResetSnapshot.cs`) reuses a live queue of the snapshot queue's name, keeping its
  `NamedQueue.Owner` and `Flags`. A snapshot never holds an exclusive queue, so a connection that declares a name
  exclusive before a restore keeps the restored queue locked to itself: another caller gets `QueueLocked` (an AMQP
  1.0 subscriber `amqp:resource-locked`), and the owner's end deletes the queue with the restored messages. A live
  auto-delete queue likewise keeps that flag over a snapshot's durable one.
- A likely shape: replace the reused queue when owner or flags differ, ending its consumers and keeping delivery
  tags rising so a stale get tag cannot settle a new delivery, with no extra `Deleted` change. Decide in an
  ADR-0146 amendment and replace the limit Amendment 8 names.

## Acceptance criteria

- [ ] `Surl.MessageBus.UnitTests`: after a restore over an exclusive queue of the same name, another connection inspects and consumes the restored queue, and the former owner's end leaves it and its messages.
- [ ] `Surl.MessageBus.UnitTests`: after a restore over a queue with other flags, declaring it with the snapshot's flags succeeds.
- [ ] ADR-0146 and `Surl.MessageBus.UnitLibrary/CLAUDE.md` state the rule, and their sentence naming this limit is gone.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-28: Created.
