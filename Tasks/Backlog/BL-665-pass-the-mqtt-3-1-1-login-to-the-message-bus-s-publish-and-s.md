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
completed:
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

- [ ] Named tests replay sessions where: an account with `sub` on `a/#` subscribes to `a/b`; an account without it
      gets SUBACK `0x80` (or the ADR's measured answer); an account without `pub` has its PUBLISH handled as the ADR
      says and the message is not retained; with no access configured every existing test passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
