---
id: BL-507
title: Serve amqp and amqps from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-506, BL-405, BL-635]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-507 — Serve amqp and amqps from the surl executable

## Goal

`surl amqp://127.0.0.1:0` and `surl amqps://127.0.0.1:0` (TLS) start the AMQP 0-9-1 broker with its shared
state. The `--aihelp` topic and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `amqps` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming AMQP 0-9-1 as its oracle, an `AiHelpExamples` entry, and the topic
  list in `AiHelpTextTests` growing by one.
- Shared message bus (Stewart, 2026-11-04; BL-622's ADR decision 1): the broker is handed the **one**
  `Surl.MessageBus` BL-635 composes for `mqtt`, not state of its own. If BL-609 has moved the composition to
  `Application/Surl.Composition.UnitLibrary`, register it there and record the changed touches in Notes.

## Acceptance criteria

- [ ] A named test shows the `amqp` broker and the `mqtt` server composed with one and the same bus instance.

- [ ] A named test finds `amqp` and `amqps` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-635; composed with the one shared bus.
- 2026-11-05: Backlog -> Doing.
