---
id: BL-533
title: Serve stomp and stomps from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-532, BL-405, BL-635]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-533 — Serve stomp and stomps from the surl executable

## Goal

`surl stomp://127.0.0.1:0` and `surl stomps://127.0.0.1:0` (TLS) start the STOMP broker with its shared state.
The `--aihelp` topic and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `stomps` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming STOMP 1.2 as its oracle, an `AiHelpExamples` entry, and the topic list
  in `AiHelpTextTests` growing by one.
- Shared message bus (Stewart, 2026-11-04; BL-622's ADR decision 1): the broker is handed the **one**
  `Surl.MessageBus` BL-635 composes for `mqtt`, not state of its own. If BL-609 has moved the composition to
  `Application/Surl.Composition.UnitLibrary`, register it there and record the changed touches in Notes.

## Acceptance criteria

- [ ] A named test shows the `stomp` broker and the `mqtt` server composed with one and the same bus instance.

- [ ] A named test finds `stomp` and `stomps` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 8): BL-609 moved the composition. Extended servers are now registered in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs`, and the shared bus is composed in
  `SurlServerComposition`. So the stomp registration and its bus-sharing test belong in Surl.Composition and its
  tests, and both are added to `touches`. BL-803 (Doing) touches Surl.Composition, so the task waits in Backlog
  until that task no longer overlaps.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-635; composed with the one shared bus.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary, which BL-803 (Doing) touches
