---
id: BL-1721
title: Add --mqtt-hold-subscription to surl and give its count to the MQTT server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1720]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Mcp.UnitLibrary, Documentation/Planning/Decisions/ADR-0014-how-the-mqtt-server-answers.md, Documentation/Planning/Decisions/README.md, Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1721 — Add --mqtt-hold-subscription to surl and give its count to the MQTT server

## Goal

`surl --mqtt-hold-subscription <count> mqtt://...` holds each MQTT 3.1.1 subscription open as ADR-0014 Amendment 1 says.

## Context

- ADR-0014 Amendment 1 (BL-1715) decides the option's name, values and meaning; BL-1720 gives the server the count.
- Follow how `--mqtt5-disconnect-after-retained` is wired: `CommandLineOptions.cs`, `SurlCommandLine.cs`,
  `ManualText.cs`, `AiHelpProse.cs` (`mqtt` topic), `ComposedProtocolServices.cs`, `SurlServerComposition.Start.cs`,
  `CommandLinePathMembers.cs`.
- The `mqtt` `--aihelp` paragraph that describes the one-shot subscribe changes in the same diff.

## Acceptance criteria

- [x] Tests show `--mqtt-hold-subscription 3` and `0` parse, a negative or non-numeric value exits 2, and the option is absent by default.
- [x] A `Surl.Composition.UnitTests` test shows a composed `mqtt` server given the option sends a live publish and then `DISCONNECT` at the count.
- [x] `--help`, `--manual` and `--aihelp mqtt` describe the option, including that curl exits 0 at the count and 28 under its own `--max-time`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Wiring: `SurlCommandLine.MqttHoldSubscriptionCount` (`int?`), read by `OptionArgumentReader.Number`, carried by
  `ComposedProtocolServices.MqttHoldSubscriptionCount` to `MqttProtocolServer`'s third constructor value;
  `Surl.Mcp`'s `CommandLinePathMembers` classes it as holding no path.
- Choice (recorded in ADR-0014 Amendment 1 decision 2): so that `surl --help --mqtt-hold-subscription` can say how
  curl ends, the option has an `OptionHelp.Explanation`, until now only given to loosening options. `--help testing`
  does not list it; `AiHelpTextTests.Answer_Testing_ListsEachExplanationAfterTheTable` now checks the `testing`
  options only. The one-line description cannot hold both exit codes.
- The manual and the explanation name curl's `-N`, so `OptionNames` in the tests lists it as curl's.
- No `AiHelpExamples` entry added: the existing `mqtt` example is unchanged and the option's use is in the prose.
- `touches` widened to ADR-0014, the decisions index and the MQTT library's `CLAUDE.md`: each said the option was
  not built yet. No task in Doing names them.
- Verified: build 0 warnings, format clean, fast tests 29216 passed, 0 failed, 3 skipped. Coverage was not
  re-measured (the new production lines add no branch). Pinned-curl proof of the held mode is BL-1722.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. surl --mqtt-hold-subscription <count> holds each MQTT 3.1.1 subscription open and ends it with DISCONNECT at the count
