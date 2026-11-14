---
id: BL-1140
title: Play PUBLISH-before-SUBACK, short-PUBLISH, DISCONNECT-malformed and excessive-remaining in the MQTT upstream test-case server half
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1139]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Conformance/Surl.Conformance.UnitLibrary, Documentation/Conformance/Upstream-Test-Case-Exclusions.md]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1140 — Play PUBLISH-before-SUBACK, short-PUBLISH, DISCONNECT-malformed and excessive-remaining in the MQTT upstream test-case server half

## Goal

The MQTT upstream test-case server half implements `PUBLISH-before-SUBACK`, `short-PUBLISH`,
`DISCONNECT-malformed` and `excessive-remaining` as BL-1138's ADR decides. Upstream tests 1194, 1195,
2207 and 3017 then run instead of skipping `servercmd-unsupported:<command>`, and pass on Windows,
Linux and macOS.

## Context

- All four skip on every platform (CI run 43629075978; local Windows reference run 2026-11-14 at
  `5a45202a`): 1194 `PUBLISH-before-SUBACK TRUE`; 1195 that and `short-PUBLISH TRUE`; 2207
  `DISCONNECT-malformed TRUE`; 3017 `excessive-remaining TRUE`.
- BL-1139 (which this depends on) gives `MqttUpstreamTestCaseReplies` the `<servercmd>` lines and
  `UpstreamTestCaseServerCommands` an `mqtt` set; add these four to both. Read BL-1138's ADR for the
  bytes; do not decide one here.
- Where: `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary` (`MqttUpstreamTestCaseReplies`,
  `MqttUpstreamTestCaseServer`, where it answers `SUBSCRIBE` and sends `PUBLISH`), and the `mqtt`
  set in `Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServerCommands.cs`.
- If 1194 or 1195 then fails only on the RETAIN flag of the first `PUBLISH`, the byte the exclusions
  file already lists for 1190, 1192, 2204, 3018 and 1640, follow ADR-0080 decision 5: add its row to
  `Documentation/Conformance/Upstream-Test-Case-Exclusions.md` only if the ADR said so, and name it
  in Notes; otherwise it is a defect to fix here.
- Stand-in: not applicable. The seam is composed only by `Surl.Conformance.UnitTests`.

## Acceptance criteria

- [ ] For each of the four commands, a fast test in `Surl.Protocol.Mqtt.UnitTests` replays the
      request bytes of its case over a fake `IConnection` and gets the packet bytes the ADR names.
- [ ] `UpstreamTestCaseServerCommands` lists the four for `mqtt` and `mqtts`, and its test pins them.
- [ ] A local Windows run of `UpstreamTestCasesAgainstSurlTests` passes 1194, 1195, 2207 and 3017
      and every case already on `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [ ] `dotnet build -warnaserror` is clean for the three projects, the fast tests are green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
