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
completed:
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

- [ ] Named `MessageBusTests` show one event per change kind above with its topic or queue, in order, from the
      recording publisher double.
- [ ] Named tests show an out-of-space rule for topic filter `a/#` refuses a publish to `a/b` with the ADR's
      outcome and publishes the fault event, while `c/d` succeeds; a rule with `times=1` fires once.
- [ ] With no publisher or plan given, every existing `Surl.MessageBus.UnitTests` test passes unchanged.
- [ ] `dotnet build Protocols/Messaging/Surl.MessageBus.UnitLibrary -warnaserror` is clean, the whole solution
      builds, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
