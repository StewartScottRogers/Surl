---
id: BL-1721
title: Add --mqtt-hold-subscription to surl and give its count to the MQTT server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1720]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Mcp.UnitLibrary]
requirement: none
created: 2026-11-25
completed:
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

- [ ] Tests show `--mqtt-hold-subscription 3` and `0` parse, a negative or non-numeric value exits 2, and the option is absent by default.
- [ ] A `Surl.Composition.UnitTests` test shows a composed `mqtt` server given the option sends a live publish and then `DISCONNECT` at the count.
- [ ] `--help`, `--manual` and `--aihelp mqtt` describe the option, including that curl exits 0 at the count and 28 under its own `--max-time`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
