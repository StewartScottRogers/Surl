---
id: BL-1217
title: Prove an MQTT message pinned upstream curl publishes reaches a Redis SUBSCRIBE in the publish-and-consume stand-in recipe
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/StandInPublishAndConsumeRecipeTests.cs, Documentation/Wiki/Surl-As-A-Stand-In.md]
requirement: FR-058
created: 2026-11-15
completed:
---
# BL-1217 — Prove an MQTT message pinned upstream curl publishes reaches a Redis SUBSCRIBE in the publish-and-consume stand-in recipe

## Goal

The publish-and-consume stand-in recipe proves that a message pinned upstream curl 8.21.0 publishes over MQTT reaches a Redis subscriber on the same server through the shared message bus.

## Context

- ADR-0126 puts Redis pub/sub on the shared bus: a channel that is a canonical topic name (no `+` or `#`, valid UTF-8) is the bus topic of the same name. The only proof today is `SurlServerCompositionTests.ComposeExtendedProtocolServers_RedisAndMqtt_HoldTheOneMessageBus`, which checks that both servers hold one bus object. No end-to-end test carries a message from one to the other (stand-in audit of 2026-11-15).
- Where: `Conformance/Surl.Conformance.UnitTests/StandInPublishAndConsumeRecipeTests.cs`. `PublishOverMqtt_ThenConsumeOverNats_ReceivesTheExactPayload` is the pattern to copy:
  - upstream curl is the publisher (`PublishOverMqttAsync`, topic `a/b`, payload `hello bus`);
  - a test-local `TcpClient` is the consumer, because upstream curl speaks no Redis;
  - the test waits with `server.WaitForEventAsync` instead of sleeping.
  Add `redis://127.0.0.1:0` to the listen URLs the class's `StartAsync` gives (extended servers are on unless `SURL_EXTENSIONS=0`).
- Redis frames (RESP2, written from the Redis protocol specification). Send `*2\r\n$9\r\nSUBSCRIBE\r\n$3\r\na/b\r\n`. The confirmation `*3\r\n$9\r\nsubscribe\r\n$3\r\na/b\r\n:1\r\n` proves the subscription exists. The delivery is `*3\r\n$7\r\nmessage\r\n$3\r\na/b\r\n$9\r\nhello bus\r\n`. With no account configured, check how the class's other consumers log in (anonymous under its options) and do the same.
- This is a pinned-curl run: `[TestCategory("Integration")]`, inconclusive where no pinned build is installed, as the class's other tests are.

## Acceptance criteria

- [ ] `StandInPublishAndConsumeRecipeTests` has `PublishOverMqtt_ThenConsumeOverRedis_ReceivesTheExactPayload`. It subscribes over Redis, publishes `hello bus` to `a/b` with pinned upstream curl 8.21.0, waits for the bus publish event, and reads the Redis `message` frame.
- [ ] The test asserts curl exited 0 and that the consumer received exactly `*3\r\n$7\r\nmessage\r\n$3\r\na/b\r\n$9\r\nhello bus\r\n`.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~StandInPublishAndConsumeRecipeTests"` passes on a machine with the pinned build, and the project builds clean with `-warnaserror`.
- [ ] `Documentation/Wiki/Surl-As-A-Stand-In.md`, section "Recipes", bullet "Publish and consume", names the new test.

## Notes

## Log

- 2026-11-15: Created.
