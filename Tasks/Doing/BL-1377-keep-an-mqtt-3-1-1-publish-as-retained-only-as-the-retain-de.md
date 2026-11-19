---
id: BL-1377
title: Keep an MQTT 3.1.1 publish as retained only as the RETAIN decision says
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1376]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-1377 — Keep an MQTT 3.1.1 publish as retained only as the RETAIN decision says

## Goal

The MQTT 3.1.1 server (`mqtt`, `mqtts`) keeps, replaces or removes a topic's retained message on a `PUBLISH`
exactly as the ADR BL-1376 records says, and `surl --aihelp mqtt` describes that behaviour and why the server
sends `DISCONNECT`.

## Context

- Finding 1 of the Surly cookbook bug report of 2026-11-18. BL-1376 decides the behaviour; read its ADR first.
  If the ADR keeps today's behaviour, this task only rewrites the `--aihelp mqtt` sentence the ADR asks for and
  ticks the code criteria as "unchanged by the ADR" in the Log.
- Where the behaviour lives: `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary/MqttPacketResponder.cs:265-279`
  (`PublishAsync` builds every `BusMessage` with `Retain = true`, comment citing ADR-0014). The RETAIN flag is
  bit 0 of the `PUBLISH` fixed header (MQTT 3.1.1 section 3.3.1.3); upstream curl 8.21.0 always sends `0x30`.
- Live bus subscribers (an `mqtt5`, NATS, STOMP, AMQP or Kafka consumer on the same `MessageBus`) still receive a
  RETAIN 0 publish; only retention changes. A `--topic <filter>;retain` declaration makes a matching publish
  retained whatever its flag (`MessageBus.Publish`, `Protocols/Messaging/Surl.MessageBus.UnitLibrary/MessageBus.cs:169-176`,
  ADR-0102 decision 3), so curl users keep a way to seed. `MqttUpstreamTestCaseServer.cs:181` publishes with
  `Retain = true` itself and is unaffected.
- Text describing today's behaviour that must change in the same diff (root `CLAUDE.md`, `--aihelp` rule):
  `MqttProtocolServer.cs:6-16` (XML doc), `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary/CLAUDE.md`
  (paragraph one), and `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs:232` ("which is kept as the topic's
  retained message"). The `messages` example in `AiHelpExamples.cs:78-84` already seeds with `--topic shop/#;retain`.
- Fast tests that publish with `0x30` and expect retention: in `Surl.Protocol.Mqtt.UnitTests`, among others
  `MqttProtocolServerTests.ServeAsync_PublishThenSubscribeFromCurl_DeliversWhatWasPublished`,
  `ServeAsync_Publish200BytesFromCurl_KeepsTheWholePayload`, `ServeAsync_EmptyPublish_RemovesTheRetainedMessage`,
  `ServeAsync_TwoServersOnOneBus_EachSubscriberReceivesWhatTheOtherServerPublished`,
  `ServeAsync_PublishToALoadedBus_WritesTheBusRetainedFile`, `ServeAsync_QualityOfService1Publish_AnswersPubackAndKeepsTheMessage`,
  `MqttFaultTests.ServeAsync_PublishOutsideTheRulesTopic_IsRetained`; and
  `Application/Surl.Composition.UnitTests/SurlServerCompositionTests.MessageBus.cs:21-26` (`ConnectPublishHiOnTDisconnect`,
  a `0x30` publish asserted retained by `ComposeProtocolServers_OneBus_IsHandedToTheMqttServerOfBothMqttAndMqtts`).
  `Surl.Composition.UnitTests` and `Surl.Cli.UnitLibrary` are touched only for that fixture byte and that one
  sentence, which the same-diff rule requires.
- Stand-in: the store is the one `MessageBus`; a change still publishes one `StoreChangedEvent` per retained
  write as today, and none for a publish that is not retained. Tests seed retained messages with `--topic`,
  `Surl.Api`'s `Messages.PublishAsync(..., retain: true)` or `surl-mcp`'s `publish_message`.

## Acceptance criteria

- [ ] New tests in `Surl.Protocol.Mqtt.UnitTests` pin the ADR's rule for a RETAIN 0 and a RETAIN 1 `PUBLISH`:
      kept or not, replacing an existing retained message or not, and an empty payload removing it or not, each
      checked through a following `SUBSCRIBE` on the same bus.
- [ ] A test in `Surl.Protocol.Mqtt.UnitTests` shows a RETAIN 0 publish on a topic a `DeclaredTopic` marks
      retained (`--topic <filter>;retain`) is delivered to a later `SUBSCRIBE`.
- [ ] Every existing fast test that relied on the old rule publishes RETAIN 1 (`0x31`) or seeds through a
      declared topic, and its name still says what it checks; `dotnet test --filter "TestCategory!=Integration"`
      is green.
- [ ] `AiHelpProse.cs`'s `mqtt` paragraph, `MqttProtocolServer`'s XML doc and the library's `CLAUDE.md` state the
      new rule and that the server sends `DISCONNECT` because upstream curl 8.21.0 exits 56 on a bare close;
      `CommandLineRunnerAiHelpTests` and `AiHelpTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean and `Surl.Protocol.Mqtt.UnitLibrary` stays at 100% line and branch
      coverage.

## Notes

- Do not touch the `Integration` tests in `Conformance/`; BL-1378 reruns them against pinned upstream curl.
- No new option and no new `SurlExitCode`.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing.
