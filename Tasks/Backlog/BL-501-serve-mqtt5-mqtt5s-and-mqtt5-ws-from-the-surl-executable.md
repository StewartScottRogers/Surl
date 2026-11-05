---
id: BL-501
title: Serve mqtt5, mqtt5s and mqtt5+ws from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-500, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-501 — Serve mqtt5, mqtt5s and mqtt5+ws from the surl executable

## Goal

`surl mqtt5://`, `surl mqtt5s://` (TLS) and `surl mqtt5+ws://` start the MQTT 5 broker with its shared state.
`surl mqtt://` still starts the original 3.1.1 server exactly as before. The `--aihelp` topic and help category
exist, and the extension switch turns all three off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `mqtt5s` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010). The broker's shared state is created once
  per run, as `MqttRetainedMessages` is for `mqtt`.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with the three schemes, an
  `AiHelpProse.TopicAbout` paragraph naming MQTT 5.0 as its oracle and saying `mqtt` stays 3.1.1, an
  `AiHelpExamples` entry, and the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `mqtt5`, `mqtt5s` and `mqtt5+ws` registered when the switch is on, and absent when it is
      off, with `mqtt` and `mqtts` registered either way.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass. `surl --aihelp mqtt` is unchanged except
      for a cross-reference, if the ADR adds one.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
