---
id: BL-1139
title: Play error-CONNACK, remlen-CONNACK and PINGRESP-as-CONNACK in the MQTT upstream test-case server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1138]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1139 — Play error-CONNACK, remlen-CONNACK and PINGRESP-as-CONNACK in the MQTT upstream test-case server half

## Goal

The MQTT upstream test-case server half receives a case's `<servercmd>` lines and implements the
three CONNACK commands as BL-1138's ADR decides. Upstream tests 1196, 2200, 2202, 2203, 1132 and 2206
then run instead of skipping `servercmd-unsupported:<command>`, and pass on Windows, Linux and macOS.

## Context

- All six skip on every platform (CI run 43629075978; local Windows reference run 2026-11-14 at
  `5a45202a`): 1196, 2200, 2202 and 2203 use `error-CONNACK 1` or `5`, 1132 `remlen-CONNACK 3`, 2206
  `PINGRESP-as-CONNACK TRUE`.
- Read BL-1138's ADR first; it fixes the bytes and how the half gets the lines. Do not decide a byte
  here.
- Where:
  - `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary`: `MqttUpstreamTestCaseReplies` takes only
    `<data>` today and its `SupportedServerCommands` is empty; add the server-command lines and the
    three commands, and apply them where `MqttUpstreamTestCaseServer` answers `CONNECT`.
  - `Conformance/Surl.Conformance.UnitTests/UpstreamTestCasesAgainstSurlTests.cs`: pass the case's
    `<servercmd>` lines where it constructs `MqttUpstreamTestCaseReplies`.
  - `Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServerCommands.cs`: `mqtt` and `mqtts`
    get a set (`Testnum` and the three) instead of `None`; update its test.
- BL-1140 adds the other four MQTT commands to the same set afterwards.
- Stand-in: not applicable. The seam is composed only by `Surl.Conformance.UnitTests`. (Surl's
  shipped refuse-login fault for MQTT, ADR-0139, is a separate feature; do not route these through it.)

## Acceptance criteria

- [ ] For each of `error-CONNACK 5`, `remlen-CONNACK 3` and `PINGRESP-as-CONNACK TRUE`, a fast test in
      `Surl.Protocol.Mqtt.UnitTests` replays the `CONNECT` bytes of its case over a fake `IConnection`
      and gets the packet bytes the ADR names.
- [ ] `UpstreamTestCaseServerCommands` lists the three for `mqtt` and `mqtts`, and its test pins them.
- [ ] A local Windows run of `UpstreamTestCasesAgainstSurlTests` passes 1196, 2200, 2202, 2203, 1132
      and 2206 and every case already on `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [ ] `dotnet build -warnaserror` is clean for the four projects, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
