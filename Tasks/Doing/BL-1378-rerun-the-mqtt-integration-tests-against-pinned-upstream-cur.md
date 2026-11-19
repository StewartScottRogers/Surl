---
id: BL-1378
title: Rerun the MQTT Integration tests against pinned upstream curl after the RETAIN change
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1377]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-1378 — Rerun the MQTT Integration tests against pinned upstream curl after the RETAIN change

## Goal

Every `Integration` test in `Conformance/Surl.Conformance.UnitTests` that drives `surl`'s MQTT 3.1.1 server
passes against pinned upstream curl 8.21.0 under the RETAIN rule BL-1376 decided and BL-1377 built.

## Context

- BL-1377 changes (or, if the ADR keeps it, confirms) whether a RETAIN 0 `PUBLISH` - which is every upstream curl
  `-d` publish, `lib/mqtt.c` line 588 at `curl-8_21_0` - is kept as retained. Tests that publish with upstream
  curl and then fetch the same topic with upstream curl relied on the old rule.
- Files to rerun and, where the ADR changed behaviour, update (all `[TestCategory("Integration")]`):
  `UpstreamCurlTalksToSurlOverMqttTests.cs` (`PublishThenSubscribe_*`, `PublishRestartSubscribe_*`),
  `UpstreamCurlSharesTopicsAcrossSurlListenersTests.cs`, `UpstreamCurlLogsInToSurlOverMqttTests.cs`
  (`PublishThenSubscribeOverMqtts_Account_SubscriberReceivesTheMessage`), `UpstreamCurlTalksToAConfiguredSurlTests.cs`
  (`MqttPublishByAnAccountWithPub_IsAccepted`, `MqttSubscribeByAnAccountWithoutSub_IsRefusedAndCurlExits8`),
  `StandInPublishAndConsumeRecipeTests.cs`, and the Curl-port ones `CurlPortBesideUpstreamCurlOverMqttTests.cs` and
  `CurlPortClientConsumesMessagesSharedAcrossSurlProtocolsTests.cs` (the Curl port is a client there only, never an
  oracle, ADR-0003).
- The usual change is to start `surl` with `--topic <filter>;retain` for the topic the test publishes and then
  fetches, as `Mqtt5Publish_ThenMqttSubscribe_PrintsTheTopicAndPayload` already does
  (`CurlPortClientConsumesMessagesSharedAcrossSurlProtocolsTests.cs:108-112`). Where a test exists to show a
  publish-then-fetch over plain `mqtt`, keep its name true: rename it if it now proves something else.
- Expected bytes and exit codes come only from the pinned upstream build in `UpstreamCurlBuilds.json`; measure
  any new case with `Record-CurlExchange.ps1`, never with the Curl port.
- Stand-in: these are the cross-protocol proofs (publish over MQTT, consume over NATS, STOMP, Redis, AMQP, Kafka,
  MQTT 5). Live delivery across the bus does not depend on retention, so those should pass unchanged; say so in
  the Log if they do.

## Acceptance criteria

- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "TestCategory=Integration&FullyQualifiedName~Mqtt"`
      passes on Windows with the pinned upstream curl 8.21.0 build, and the Log records the pass and fail counts.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~StandInPublishAndConsumeRecipeTests|FullyQualifiedName~UpstreamCurlTalksToAConfiguredSurlTests"`
      passes.
- [ ] Every test that fetches a message an upstream curl `-d` published either starts `surl` with
      `--topic <filter>;retain` or asserts the empty fetch (exit 0, no output) the ADR now gives, and its name says
      which.
- [ ] No expected byte or exit code in a changed test comes from the Curl port.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- If the ADR kept today's behaviour, run the two filters, record the results in the Log, and finish.
- A lane without the pinned upstream build cannot run these; that is a reason to block for the build, not to
  substitute another curl.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing.
