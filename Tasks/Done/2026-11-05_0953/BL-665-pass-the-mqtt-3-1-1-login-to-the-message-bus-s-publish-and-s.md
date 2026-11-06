---
id: BL-665
title: Pass the MQTT 3.1.1 login to the message bus's publish and subscribe access check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-664, BL-635]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-665 — Pass the MQTT 3.1.1 login to the message bus's publish and subscribe access check

## Goal

The original MQTT 3.1.1 server passes the connection's logged-in account and scheme to the bus on every PUBLISH
and SUBSCRIBE and answers a refusal as BL-642's ADR measured against upstream curl.

## Context

- Decision: BL-642's ADR decision 3 (MQTT 3.1.1: a refused SUBSCRIBE as SUBACK return code `0x80` - MQTT 3.1.1
  section 3.9.3 - and a refused PUBLISH as the ADR says, since 3.1.1 has no negative PUBACK; both measured against
  pinned upstream curl 8.21.0 with curl's exit code). ADR-0014 (how the server answers), ADR-0032 section 5 (MQTT
  policy: the checked account), BL-664's access API.
- Where: `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary` (`MqttPacketResponder`, `MqttConnectJudge`), on the
  bus since BL-635.
- Oracle: pinned upstream curl only. `UpstreamCurlTalksToSurlOverMqttTests` and `UpstreamCurlLogsInToSurlOverMqttTests`
  must pass unchanged.

## Acceptance criteria

- [x] Named tests replay sessions where: an account with `sub` on `a/#` subscribes to `a/b`; an account without it
      gets SUBACK `0x80` (or the ADR's measured answer); an account without `pub` has its PUBLISH handled as the ADR
      says and the message is not retained; with no access configured every existing test passes unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Tests: `MqttTopicAccessTests` (`ServeAsync_AnAccountWithSubOnAHashFilter_SubscribesToATopicUnderItAndGetsItsRetainedMessage`,
  `ServeAsync_AnAccountWithoutSub_GetsSubAck0x80ThenDisconnectAndNoRetainedMessage` - R12's bytes,
  `ServeAsync_ALoginAcceptedUnchecked_IsAnAnonymousCallerAndIsRefusedTheSubscribe`,
  `ServeAsync_ASubscribeWithAGrantedADeniedAndAnInvalidFilter_AnswersEachInOrder`,
  `ServeAsync_AnAccountWithoutPub_HasItsQos0PublishDroppedNotedAndNotRetained` - R14's bytes,
  `ServeAsync_AnAccountWithoutPub_HasItsQos1PublishAcknowledgedButNotRetainedAndTheConnectionGoesOn`,
  `ServeAsync_AnAccountWithPub_HasItsPublishRetained`) and
  `MqttBusRetainedMessagesTests.Read_AnAccountGrantedOneFilterOfTwo_SaysWhichAndReadsOnlyTheGrantedOnesMessages`.
  No existing test's expectation changed (only `MatchingAny` calls renamed to `Read(...).Matching`); 171 MQTT tests
  pass, the library measures 100% line and branch coverage (cobertura).
- Choices (Claude, under Stewart's delegation; ADR-0102 decision 3.4 already decides the answers, so no new ADR):
  - The account is the `CONNECT` user name only when the policy answers `Accepted`; `AcceptedUnchecked`
    (`--allow-anonymous`) is an anonymous caller, since unchecked credentials prove no account.
  - The scheme is not passed: BL-664 made access global (the bus takes an account only), and MQTT's names are mapped
    by `MqttTopicNames` already.
  - A refused QoS 1 or 2 publish is still answered `PUBACK`/`PUBREC`: 3.1.1 has no negative acknowledgement and the
    ADR says the connection goes on; withholding it would leave the client retrying. Nothing is saved for it.
  - `MqttBusRetainedMessages.MatchingAny` became `Read(bus, account, filters)`, returning `MqttRetainedReading`
    (granted per filter, matching messages); duplicate filters are no longer collapsed before subscribing, which
    changes nothing visible (the matching set is keyed by topic).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The MQTT 3.1.1 server passes its checked account to the bus on every publish and subscribe: a refused filter is SUBACK 0x80 then DISCONNECT, a refused publish is dropped and noted
