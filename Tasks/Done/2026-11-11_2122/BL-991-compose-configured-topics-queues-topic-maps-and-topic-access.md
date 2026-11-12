---
id: BL-991
title: Compose configured topics, queues, topic maps and topic access into the message bus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-990, BL-664, BL-665]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-991 — Compose configured topics, queues, topic maps and topic access into the message bus

## Goal

The topics, queues, topic maps and topic access in `SurlCommandLine` (from BL-990) configure the one message bus
every broker in the process shares, with each widening setting warned at start.

## Context

- Second of the three tasks BL-666 was split into (see BL-990).
- Decisions: ADR-0101 (which setting widens access, warned per ADR-0032 section 9) and ADR-0102 decisions 3, 4
  and 5.
- Builds on: BL-664 (configuration in the bus), BL-665 (MQTT passes its caller), BL-646 (group expansion). The
  extended brokers get the same bus when their "Serve … from the surl executable" tasks register them; nothing here
  waits on them.
- Where: `Application/Surl.Composition.UnitLibrary`, where BL-635 composes the bus.
- Stand-in: the message bus is the shared store; its seeding and events are BL-805's and ADR-0127's.

## Acceptance criteria

- [x] Named tests in `Application/Surl.Composition.UnitTests` show topics, queues, maps and access (with an
      `@group`) reaching the composed bus.
- [x] Named tests show each widening setting's warning written at start.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

- Built `MessageBusComposition` (`Compose`, `WriteWarnings`, `WriteStartLines`) on BL-988's
  `ContentShareComposition` pattern. `ComposeAsync` composes it after the shares and before the lock, and hands
  the `MessageBusConfiguration` through `LoadServiceStateAsync` to `LoadMessageBusAsync` (new optional last
  parameters, so existing callers are unchanged). `ComposedServing` reads `Stores.Bus.Configuration` for the
  `@everyone` warning (after the share one) and the `-v` notes (after the shares').
- Tests: `MessageBusCompositionTests` (every refusal, groups expanded with union of rights, the composed
  configuration accepted by a real bus, warnings and notes), and `SurlServerCompositionTests.Topics.cs`
  (`ComposeAsync_TopicsQueuesMapsAndAccess_ReachTheOneBusTheMqttServerHolds`,
  `ComposeAsync_TopicAccessMemberNamingNothing_IsRefusedBeforeAnyListenerBinds`,
  `ServeAsync_TopicAccessGrantingEveryone_WritesItsWarningAndVerboseNotes`).
- Coverage: `MessageBusComposition` measured at line-rate 1 and branch-rate 1 with the MSTest coverage collector.
- Decisions (refusal texts and order, union of rights, note punctuation) recorded as ADR-0102 section 3.6.
  Added that ADR file to `touches`: no task in Doing names it.
- `--topic-access` is the only widening setting of the four (ADR-0101 decision 4's table: none of the others
  widens), and only through `@everyone`, so that is the one warning.
- The bus's `MaxTopics` (10000) cannot be passed by the declared topics, since an option is bounded at 10000
  uses by `Surl.Cli`; the queue bound (1000) can, so it is refused at start rather than throwing in the bus.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. --topic, --queue, --topic-map and --topic-access configure the one message bus, refused at start when invalid, @everyone warned
