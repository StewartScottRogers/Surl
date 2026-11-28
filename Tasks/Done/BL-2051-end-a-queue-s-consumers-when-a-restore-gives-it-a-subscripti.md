---
id: BL-2051
title: End a queue's consumers when a restore gives it a subscription description
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2051 — End a queue's consumers when a restore gives it a subscription description

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

- [x] `Surl.MessageBus.UnitTests`: a consumer of an undescribed queue, restored over with a snapshot in which that name carries a description, receives none of the restored messages, and they stay ready.
- [x] `Surl.MessageBus.UnitTests`: a restore that leaves a queue's description unchanged keeps its consumers, as before.
- [x] ADR-0146 and `Surl.MessageBus.UnitLibrary/CLAUDE.md` state the rule, and their sentence naming this limit is gone.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Rule (ADR-0146 Amendment 8, decided by Claude under Stewart's delegation): a restore that changes a queue's
  description in any direction ends its consumers and get holders before refilling; unchanged keeps them. Code:
  `EndConsumersIfDescriptionChanges` (`MessageBus.SeedResetSnapshot.cs`); `Delete` shares
  `EndConsumersDroppingTheirMessages`.
- Tests: `MessageBusRestoreDescriptionTests`, run before the fix and failing on the consumers that still received.
- Pipeline: the plan, tests and implementation were done in the session, not delegated, because the change is
  about 20 lines in one library. Review was delegated to `code-reviewer`. Conformance stage skipped: only a test
  host's restore reaches this, so no option, exit code or byte upstream curl exchanges changes.
- Review: fixed the false "no client is told, as when a queue is deleted" (AMQP 0-9-1 sends `basic.cancel` on its
  own delete) and the auto-delete comments. The older reuse of an exclusive queue's owner and flags is named as a
  limit and filed as BL-2083; the Amqp1 library comments, outside `touches`, as BL-2084.
- Final run: build 0 warnings, format clean, fast tests green in 83 assemblies (30732 passed), bus library 100%
  line and branch, 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. A restore that changes a queue's subscription description ends its consumers and get holders, so the restored messages stay ready (ADR-0146 Amendment 8)
