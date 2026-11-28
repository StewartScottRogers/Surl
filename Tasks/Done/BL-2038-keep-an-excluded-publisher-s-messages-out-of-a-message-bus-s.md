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
completed: 2026-11-27
---
# BL-2038 — Keep an excluded publisher's messages out of a message bus subscription queue as ADR-0146 decides no-local

## Goal

`Surl.MessageBus` offers `BusConnection.PublisherName` and an `excludedPublisherName` parameter on
`BusConnection.DeclareQueue` (ADR-0146 Amendment 3), so a topic publish through a connection of that name is
never copied into the subscription queue.

## Context

- Found while working BL-1990 (2026-11-27): a live subscription takes `noLocal`
  (`BusConnection.Subscribe`; `BusSubscription.KeepsFrom`), a queue has no equivalent.
- Read ADR-0146 Amendment 3 (BL-2037) first: items 2 to 6 say whose publishes are withheld, that the bus
  withholds at copy-in, the two additions, the redeclare rule, and the `SURL-BUS-QUEUES-3` index written only
  when a persisted queue excludes a publisher. Do exactly that.
- Start at `MessageBus.Accept` and `CopyIntoBoundQueues` (`MessageBus.cs`, `MessageBus.Queues.cs`), the batch
  publish (`MessageBus.Batch.cs`), `MessageBus.SubscriptionQueues.cs`, and the persistence and snapshot files.
- Stand-in: the shared message bus. State the amendment adds to a queue is reset, snapshotted and restored with
  it; a withheld copy publishes no store change on the event stream. No new fault, and the out-of-space fault is
  not asked for a withheld copy (`MessageBus.Events.cs` collects the bound queue names it asks for).
- BL-2039 uses both from the AMQP 1.0 container.

## Acceptance criteria

- [x] `Surl.MessageBus.UnitTests`: a publish the amendment says is withheld is never delivered from the subscription queue, through both a single publish and a batch publish, raises no store change or fault and is in none of the result's queue counts; any other publish is delivered.
- [x] `Surl.MessageBus.UnitTests`: a queue excluding no publisher receives its own connection's publishes as before; a matching redeclare naming an excluded publisher sets it on an existing queue; a declare naming one with no subscription description throws `ArgumentException`.
- [x] `Surl.MessageBus.UnitTests`: the excluded publisher name survives a save and load and a snapshot and restore, and a bus saved without one is written as before and still loads.
- [x] `Surl.MessageBus.UnitLibrary/CLAUDE.md` states the rule.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Built as ADR-0146 Amendment 3 items 2 to 6 say. `NamedQueue.TakesCopyOf` is the one rule that copy-in
  (`CopyIntoBoundQueues`), the batch's bound check (`BatchPlan`) and the bound-queue fault ask
  (`BoundQueueNamesRefusedByFault`) all use. Tests are in `MessageBusExcludedPublisherTests`.
- Choices taken: a redeclare naming a different excluded publisher replaces the one the queue has; setting
  one raises no store change (a redeclare raises none today) and marks the queues for saving only when it
  changes. A `SURL-BUS-QUEUES-3` index giving a publisher to a queue with no description is refused as
  malformed. `MessageBusFilesTests`' "Another header" row moved from `-3` to `-4`.
- Pipeline: the amendment already is the plan, so no `protocol-architect`, `test-writer` or
  `protocol-implementer` run; tests were written first (they failed to compile against the missing API),
  then the code, in the lane's session. `code-reviewer` reviewed it; its findings (one untested branch,
  stale doc lines, three weak test assertions) are fixed. No conformance run: no option, exit code or
  wire byte changes. Docs updated in the same diff.
- Gates (2026-11-27): build clean with warnings as errors, `dotnet format --verify-no-changes` clean, fast
  tests green (645 in `Surl.MessageBus.UnitTests`), `Measure-CodeQuality.ps1` for the library 100% line,
  100% branch, no failing member.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. BusConnection.PublisherName and DeclareQueue's excludedPublisherName keep that publisher's topic publishes out of the subscription queue, persisted and snapshotted (ADR-0146 Amendment 3)
