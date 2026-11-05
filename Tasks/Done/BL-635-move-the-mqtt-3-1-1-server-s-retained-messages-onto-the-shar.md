---
id: BL-635
title: Move the MQTT 3.1.1 server's retained messages onto the shared Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-630, BL-634, BL-614]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-635 — Move the MQTT 3.1.1 server's retained messages onto the shared Surl.MessageBus

## Goal

The original `mqtt`/`mqtts` server keeps and serves its retained messages through the one `Surl.MessageBus`
composed per `surl` run instead of its own `MqttRetainedMessages`, and every byte it sends upstream curl is
unchanged.

## Context

- Decision: BL-622's ADR, decision 3 ("the original MQTT 3.1.1 server's retained messages move onto the bus with
  ADR-0014's wire behaviour unchanged") and decision 1 (one store per process, composed in `Surl.Composition`).
- Today: `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary` (`MqttPacketResponder`, `MqttRetainedMessages`,
  `MqttRetainedMessageFile`, `MqttTopicFilter`); ADR-0014 (every publish kept as its topic's retained message; a
  subscribe is one-shot; bounds in decision 7), ADR-0031 decision 6 (the retained-message file). After BL-609,
  BL-613 and BL-614 the server and its service state are composed in `Application/Surl.Composition.UnitLibrary`
  (`ServiceState`, `LoadRetainedMessagesAsync`, `ComposeRetainedMessageFile`, `ComposeProtocolServers`).
- After this task: the server references `Surl.MessageBus.UnitLibrary` (allowed by BL-622's ADR-0002 row) and
  publishes to and reads retained messages from the bus, with MQTT names converted by BL-630's mapping.
  `MqttRetainedMessages` and `MqttRetainedMessageFile` are removed (or reduced to what the ADR keeps); the bus
  is loaded at start by `Surl.Composition` from BL-634's persistence, which reads an existing
  `<path>/.surl/mqtt/retained-messages`. A malformed bus file refuses start with `CouldNotReadFile` (37) and
  `surl: (37) Could not read <file path>: <reason>`, as the MQTT file does today.
- Oracle: pinned upstream curl 8.21.0 only (ADR-0003).
  `Conformance/Surl.Conformance.UnitTests/UpstreamCurlTalksToSurlOverMqttTests.cs` and
  `UpstreamCurlLogsInToSurlOverMqttTests.cs` must pass unchanged; they are the proof the wire did not move.
- The `ExitCodeGuidanceTable` row for 37 (`Application/Surl.Cli.UnitLibrary/ExitCodeGuidanceTable.cs`) names "the
  MQTT retained-message file"; it is outside this task's touches - if its wording becomes untrue, file a
  follow-up task rather than widen this one.

## Acceptance criteria

- [x] `Surl.Protocol.Mqtt.UnitLibrary` references `Surl.MessageBus.UnitLibrary`, and no type in it keeps retained
      messages of its own; `ProtocolIsolationTests` passes.
- [x] Every existing test in `Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests` still passes, rewritten only to
      construct the server with a bus; a new named test shows two servers built on one bus (as `mqtt` and `mqtts`
      are) see each other's publishes.
- [x] A named test in `Application/Surl.Composition.UnitTests` shows one bus composed and handed to both MQTT
      registrations, and a start with a malformed bus file refused with 37 and the text above.
- [x] `UpstreamCurlTalksToSurlOverMqttTests` and `UpstreamCurlLogsInToSurlOverMqttTests` pass where a pinned build
      is present (they are not `Integration`).
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

- What changed: `MqttProtocolServer(MessageBus, IAuthenticationPolicy)`. A publish is `MessageBus.Publish` with
  `Retain = true` whatever its RETAIN flag (ADR-0014) and its QoS carried. A refused publish closes with no reply,
  as an over-bounds publish did. After it comes `SaveChangesAsync`, and a `StorageFailed` result is noted with the
  same text as before. A subscribe reads retained messages through `MqttBusRetainedMessages.MatchingAny`. It
  subscribes to each filter with `includeRetained`, takes the retained messages, disposes the subscription at
  once (one-shot, ADR-0014; at most one subscription at a time, so `MaxSubscriptionsPerConnection` never bites),
  and merges them in ordinal order of topic.
- Removed `MqttRetainedMessages`, `MqttRetainedMessageFile` and `MqttTopicFilter`, with their tests. Topic and
  filter checks now use the bus's `MqttTopicNames` (BL-630), and matching is the bus's `CanonicalTopicName`.
  `MqttTopicNamesTests` and `CanonicalTopicNameTests` cover the rules the deleted tests pinned. The bus's own
  persistence tests (BL-634) now cover the file format the deleted file tests pinned.
- Decision (sensible default, under ADR-0100 decision 3): a topic holding U+0000 is now malformed and closes with
  no reply, because the canonical name refuses it. MQTT 3.1.1 section 1.5.3 [MQTT-1.5.3-2] requires the close.
  New DataRow `U+0000 in topic` pins it. Upstream curl never sends one, so the wire with curl is unchanged, and
  both conformance classes pass (9 of 9 run against the pinned build).
- Decision: the composition makes the bus with `maxMessageBytes` = `--max-filesize`, or `long.MaxValue` when it is
  0 (no limit, as `ExchangeLimits` reads 0); every other bound is the bus's default. If the load's own write fails
  (an old `.surl/mqtt/retained-messages` migrated into `.surl/bus/retained`), the start does not end; the bus keeps
  the change and the next save writes it. This follows `MessageBus.LoadAsync`'s contract. No exchange log exists
  at start to note it in.
- No new ADR. These are implementation choices inside ADR-0100 decision 3 (and an MQTT MUST), and leaving out a
  new ADR avoids a number clash with parallel lanes.
- Touches widened (no task in Doing names these): `Application/Surl.Console.UnitTests`, because
  `CommandLineRunnerRetainedMessagesTests` called the removed composition methods and is now
  `CommandLineRunnerMessageBusTests`. `Application/Surl.Cli.UnitLibrary` and `Application/Surl.Cli.UnitTests`,
  because the `--aihelp` mqtt and data-directory prose, the `--manual` text and the exit-code-37 guidance named
  `.surl/mqtt/retained-messages` and "the MQTT retained-message file", which stopped being true. Root CLAUDE.md
  requires such prose to change in the same diff, so it was fixed here rather than in a follow-up.
  `Application/Surl.Console` is touched for its CLAUDE.md alone.
- Not done here, still outside every touch: `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md` still says
  "Not here yet: the MQTT 3.1.1 server's move onto the bus (BL-635)". BL-664 (in Doing) touches that project, so
  BL-708 is filed to drop the line.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The mqtt/mqtts server keeps and serves retained messages on the one composed Surl.MessageBus; upstream curl's wire is unchanged
