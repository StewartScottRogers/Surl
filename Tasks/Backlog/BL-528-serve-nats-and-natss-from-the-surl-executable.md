---
id: BL-528
title: Serve nats and natss from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-527, BL-405, BL-635, BL-770]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-528 — Serve nats and natss from the surl executable

## Goal

`surl nats://127.0.0.1:0` and `surl natss://127.0.0.1:0` start the NATS server, with TLS upgrade available on
`natss` as BL-524's ADR decides. The `--aihelp` topic and help category exist, and the extension switch
turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`. TLS
  upgrade availability is passed as `isTlsUpgradeAvailable` is for the mail servers.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming the NATS client protocol as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.
- Shared message bus (Stewart, 2026-11-04; BL-622's ADR decision 1): the server is handed the **one**
  `Surl.MessageBus` BL-635 composes for `mqtt`, not state of its own. If BL-609 has moved the composition to
  `Application/Surl.Composition.UnitLibrary`, register it there and record the changed touches in Notes.

## Acceptance criteria

- [ ] A named test shows the `nats` server and the `mqtt` server composed with one and the same bus instance.

- [ ] A named test finds `nats` and `natss` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 8): BL-609 has moved extended-server composition to `Application/Surl.Composition.UnitLibrary`
  (`ExtendedProtocolServers.Registrations`, beside the `s3` entry). The `nats`/`natss` registration, its project
  reference to `Surl.Extended.Protocol.Nats.UnitLibrary` and the shared-bus test belong there and in
  `Surl.Composition.UnitTests`, so both are now in `touches`. BL-803 (in Doing) touches both, so this task went
  back to Backlog until it no longer overlaps.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-635; composed with the one shared bus.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and Surl.Composition.UnitTests (where extended servers now register), which BL-803 in Doing touches
