---
id: BL-629
title: Create Surl.MessageBus with the shared topic space, fan-out and retained messages
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-622]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Surl.slnx, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-629 — Create Surl.MessageBus with the shared topic space, fan-out and retained messages

## Goal

The horizontal message bus BL-622's ADR decides exists as a library with its `.UnitTests` twin, holding one topic
space in memory: publish to a canonical topic, subscribe with a filter and receive every later matching message,
and one retained message per topic, all bounded.

## Context

- Decision: BL-622's ADR, decision 3 (name, folder, references, canonical topic names, retained semantics,
  bounds and typed outcomes). Planning names: `Protocols/Messaging/Surl.MessageBus.UnitLibrary` and
  `Protocols/Messaging/Surl.MessageBus.UnitTests`, namespace `Surl.MessageBus`; if the ADR names it differently,
  use its name and record that in Notes.
- New projects go in `Surl.slnx` inside the `/Protocols/Messaging/` solution folder in ordinal order (ADR-0079
  decision 4). `Directory.Build.props` makes the library AOT-compatible with no csproj setting.
- Isolation: add the library's row to `HorizontalLibraries` in
  `Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs` with exactly the references the
  ADR's ADR-0002 row allows (planning: `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Content.UnitLibrary`).
- Model to follow: `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary/MqttRetainedMessages.cs` (bounds as
  constructor parameters with `Default…` constants, a refusal past a bound) and `Protocols/Mail/Surl.MailStore`'s
  typed outcomes (ADR-0050 decision 3: a refusal a peer can cause is a value, not an exception). No static state;
  inject `TimeProvider` for anything timed. Thread-safe: several connections publish and subscribe at once.
- Out of scope (own tasks): protocol name mapping, queues, per-topic logs, persistence, moving the MQTT server
  onto the bus, access checks.
- Rules: `.claude/rules/csharp-style.md`, `.claude/rules/testing.md`; MSTest only; BCL only.

## Acceptance criteria

- [x] Both projects exist, are listed in `Surl.slnx` under `/Protocols/Messaging/`, and
      `ProtocolIsolationTests` passes with the new row.
- [x] Named tests show: a subscriber whose filter matches receives a published message with its payload bytes
      exact and its properties; a non-matching subscriber receives nothing; a subscription disposed receives
      nothing more; a retained publish replaces the topic's retained message and an empty retained payload
      removes it, as BL-622's ADR says; a new subscription is offered the matching retained messages; each bound
      reached returns the ADR's typed outcome and stores nothing; messages to one subscriber keep publish order.
- [x] `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md` says what the library holds and who may
      reference it.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library has 100% line and branch coverage.

## Notes

- Names as ADR-0100 decision 3.1 gives them: `Surl.MessageBus.UnitLibrary`, namespace `Surl.MessageBus`, main type `MessageBus`.
- API: `MessageBus.Connect()` gives a `BusConnection` (counts its subscriptions against `MaxSubscriptionsPerConnection`, ends them all on dispose); `BusConnection.Subscribe(filter, deliver, group, includeRetained)` returns a `BusSubscribeResult`; `MessageBus.Publish(BusMessage)` returns a `BusPublishResult` with the number of subscriptions handed the message (Redis's `PUBLISH` reply, decision 3.8). Fan-out groups (decision 3.3) are in: one member per (group, filter), round robin.
- Delivery is a callback, not a per-subscription queue: the server decides how to queue and bound its own outgoing messages (ADR-0093 decision 3 keeps offline queues in the broker), so the bus adds no unbounded buffer. Deliveries are serialised in publish order through one waiting queue and run outside the bus lock, so a callback may publish or subscribe without deadlock; a nested publish is delivered after the message being delivered. A throwing callback's exception reaches the delivering publisher and later publishes still deliver.
- Added two outcomes the ADR's table does not list, `InvalidTopic` and `InvalidFilter`: a non-canonical name reaching the bus is a refusal a peer can cause, so a value, not an exception (ADR-0050 decision 3). Servers normally refuse first in their own words (BL-630).
- A retained publish refused by a bound is refused entirely (no fan-out either): ADR-0100 3.6 says past a bound nothing changes. An empty retained payload removes the retained message and is still delivered live (MQTT 3.1.1 and 5.0 3.3.1.3). A message already expired at publish is dropped silently with `Succeeded`, 0 delivered.
- Retained offered on subscribe is a snapshot taken under the same lock that registers the subscription, so every later publish reaches it live and none is lost between the two. Expired retained messages are purged when read.
- Filter matching is a dynamic-programming match (levels x levels), so a filter of many `#` levels cannot cause exponential backtracking.
- The library references neither Abstractions nor Content yet; the isolation row allows both for persistence (BL-634). `ITopicNameLookup` (decision 3.10) is left to BL-630/BL-664, which first need it.
- Coverage of `Surl.MessageBus.UnitLibrary`: 100% line, 100% branch (101 tests).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.MessageBus holds one bounded topic space: canonical filters, ordered fan-out with groups, retained messages, typed outcomes
