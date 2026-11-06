---
id: BL-501
title: Serve mqtt5, mqtt5s and mqtt5+ws from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-500, BL-405, BL-635]
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

- Decision: ADR-0093 (BL-494), decisions 0, 7 and 9. Decision 9 adds the negatable flag
  `--mqtt5-disconnect-after-retained` (category `mqtt5`, default off): after SUBACK and the retained messages
  it sends DISCONNECT `0x00` and closes. It needs its `--aihelp` facts like any option.
- **Amended by the shared-stores decision (Stewart, 2026-11-04; BL-622's ADR amends ADR-0093 decision 0):** the
  broker is handed the **one** `Surl.MessageBus` BL-635 composes for `mqtt` and `mqtts`, not state of its own, so
  its retained messages persist where the bus persists them (`<path>/.surl/bus/`, BL-634), not in
  `<path>/.surl/mqtt5/retained-messages`. Only session state stays the broker's. If BL-609 has moved the
  composition to `Application/Surl.Composition.UnitLibrary`, register the broker there and record the changed
  touches in Notes.

## Acceptance criteria

- [ ] A named test shows the `mqtt5` broker and the `mqtt` server composed with one and the same bus instance.

- [ ] A named test finds `mqtt5`, `mqtt5s` and `mqtt5+ws` registered when the switch is on, and absent when it is
      off, with `mqtt` and `mqtts` registered either way.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass. `surl --aihelp mqtt` is unchanged except
      for a cross-reference, if the ADR adds one.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-635; composed with the one shared bus, no private retained file.
- 2026-11-05: Backlog -> Doing.
