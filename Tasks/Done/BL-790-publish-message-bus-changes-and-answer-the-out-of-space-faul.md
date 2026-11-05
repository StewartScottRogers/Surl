---
id: BL-790
title: Publish message bus changes and answer the out-of-space fault in Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-790 — Publish message bus changes and answer the out-of-space fault in Surl.MessageBus

## Goal

`MessageBus` publishes every change it makes - a message published (with its canonical topic and the publishing
scheme), retained or cleared, delivered to a subscription or queue consumer, acknowledged, a queue declared or
deleted, a log appended - on the event stream, and answers a publish with its existing full outcome when an
out-of-space fault rule matches the topic or queue, so a test can wait for a message published over MQTT and make
publishing fail on purpose.

## Context

- Decision: BL-822's ADR decisions 1 (`StoreChangedEvent`'s bus fields) and 4 (out of space: scope by topic
  filter or queue; which outcome).
- Contracts: BL-823 (`ISurlEventPublisher`, `IFaultPlan`).
- Where: `Protocols/Messaging/Surl.MessageBus.UnitLibrary` (`MessageBus.cs`, `MessageBus.Queues.cs`,
  `MessageBus.Logs.cs`). It references only `Surl.Content` today: add the project reference to
  `Foundation/Surl.Protocol.Abstractions.UnitLibrary` (allowed: every horizontal library may reference the
  contracts). Optional constructor parameters defaulting to the null objects; BL-803 passes the real ones.
- Out of space answers `MessageBusOutcome.StoreFull` (or `QueueFull` for a queue, as the ADR says), which every
  broker already words, so no broker changes here.
- An event carries the canonical topic name (ADR-0100 decision 3.2), queue, payload length and the publishing
  scheme, and the payload only if the ADR says so (bounded).
- Stand-in: store = the message bus (ADR-0100 decision 1); publishes store changes; seeding and reset are BL-801.

## Acceptance criteria

- [x] Named `MessageBusTests` show one event per change kind above with its topic or queue, in order, from the
      recording publisher double.
- [x] Named tests show an out-of-space rule for topic filter `a/#` refuses a publish to `a/b` with the ADR's
      outcome and publishes the fault event, while `c/d` succeeds; a rule with `times=1` fires once.
- [x] With no publisher or plan given, every existing `Surl.MessageBus.UnitTests` test passes unchanged.
- [x] `dotnet build Protocols/Messaging/Surl.MessageBus.UnitLibrary -warnaserror` is clean, the whole solution
      builds, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

Delivered (dark factory lane 1, 2026-11-05). `MessageBus` takes optional `ISurlEventPublisher events` and
`IFaultPlan faults` (last constructor parameters, defaulting to the discarding publisher and the empty plan);
the library now references `Surl.Protocol.Abstractions.UnitLibrary`. New `MessageBus.Events.cs`; tests in
`MessageBusEventTests` (10) and `MessageBusOutOfSpaceTests` (7), with a `SpendingFaultPlan` double. Whole
solution builds with no warnings, every fast test is green (MessageBus 486), and the library's line and branch
coverage is 100% (cobertura from `dotnet test --collect "Code Coverage"`).

Choices, all within ADR-0124 decisions 1 and 4. They are recorded here and in the library's `CLAUDE.md` rather
than in a new ADR, because they apply decisions the ADR already made, and Documentation is outside this task's
`touches`. Decided by Claude under Stewart's delegation:

- **Names.** A topic change is named by its canonical topic, and a queue change by
  `MessageBus.QueueChangeName` = `$queue/<name>`, the form `--topic-access` already uses. That way a topic
  and a queue with the same name never collide, and an out-of-space rule names a queue as `path=$queue/q`.
- **No scheme, no payload.** `StoreChangedEvent` has no scheme or payload field (BL-823's contract). Changing
  it would touch the shared contract, so the event carries the name and the payload length only.
  `ExchangeId` is null because no bus call is told its exchange. Filed **BL-865** to pass the exchange (and so
  the scheme) through `BusConnection`.
- **Change kinds.** `Published` is a topic publish after its log append: the log append and the publish are
  one event, not two. `Written`/`Deleted` on a topic mean the retained message was set or was cleared/expired.
  `Written` with a null byte count means `CreateLog` made an empty log. `Delivered` is published per live
  subscription or consumer that was handed the message, and per get. `Enqueued` and `Acknowledged` are as
  named. `Deleted` with a byte count means a reject without requeue, or a purge that removed anything (one
  event with the total). `Written`/`Deleted` with a null byte count mean a queue was declared or deleted.
- **Order.** The retained change comes before `Published`, and `Published` comes before `Enqueued` into
  bound queues. Delivery events follow from the delivering thread.
- **Out of space.** For a topic publish the outcome is `StoreFull`; for a direct `PublishToQueue` it is
  `QueueFull`. Both are outcomes every broker already words. The plan is asked at `FaultStage.StoreWrite`
  with the caller's account and the path, outside the lock, and only for a write that would happen: a message
  not already expired, and a queue that exists. The bus publishes `FaultAppliedEvent` naming the path, the
  same choice lane 6 made for the mail store in BL-788. The bus takes only an answer the plan completes at
  once; a pending answer (a slow hook) is served as no rule, as ADR-0124 decision 5 serves a hook that times
  out.
- **"Topic filter `a/#`".** ADR-0124 decision 4's `path` matches equal or as a segment prefix, so the rule
  `path=a` covers exactly what `a/#` covers: `a` and everything under it. The tests use `path=a` with a plan
  double that matches as the ADR says. `ScriptedFaultPlan` matches exactly and never spends `times`, so it
  could not show either behaviour.
- **Not covered, filed as BL-866:** drops by queue expiry and by log retention are not published yet, and a
  topic publish's copy into a bound queue does not consult a queue-scoped rule.
- `dotnet format` also normalised the final newline of `BusGetResult.cs`, `BusQueueInspectResult.cs` and
  `BusQueueRemovalResult.cs` (whitespace only).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. MessageBus publishes every topic, retained, delivery, queue and acknowledgement change on the event stream and answers out-of-space rules with StoreFull or QueueFull
