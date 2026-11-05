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
completed:
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

- [ ] Both projects exist, are listed in `Surl.slnx` under `/Protocols/Messaging/`, and
      `ProtocolIsolationTests` passes with the new row.
- [ ] Named tests show: a subscriber whose filter matches receives a published message with its payload bytes
      exact and its properties; a non-matching subscriber receives nothing; a subscription disposed receives
      nothing more; a retained publish replaces the topic's retained message and an empty retained payload
      removes it, as BL-622's ADR says; a new subscription is offered the matching retained messages; each bound
      reached returns the ADR's typed outcome and stores nothing; messages to one subscriber keep publish order.
- [ ] `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md` says what the library holds and who may
      reference it.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library has 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
