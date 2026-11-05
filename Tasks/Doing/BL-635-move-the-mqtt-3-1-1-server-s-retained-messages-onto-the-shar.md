---
id: BL-635
title: Move the MQTT 3.1.1 server's retained messages onto the shared Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-630, BL-634, BL-614]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] `Surl.Protocol.Mqtt.UnitLibrary` references `Surl.MessageBus.UnitLibrary`, and no type in it keeps retained
      messages of its own; `ProtocolIsolationTests` passes.
- [ ] Every existing test in `Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests` still passes, rewritten only to
      construct the server with a bus; a new named test shows two servers built on one bus (as `mqtt` and `mqtts`
      are) see each other's publishes.
- [ ] A named test in `Application/Surl.Composition.UnitTests` shows one bus composed and handed to both MQTT
      registrations, and a start with a malformed bus file refused with 37 and the text above.
- [ ] `UpstreamCurlTalksToSurlOverMqttTests` and `UpstreamCurlLogsInToSurlOverMqttTests` pass where a pinned build
      is present (they are not `Integration`).
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
