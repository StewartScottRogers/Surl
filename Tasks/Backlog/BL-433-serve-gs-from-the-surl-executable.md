---
id: BL-433
title: Serve gs from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-432, BL-405, BL-655]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-433 — Serve gs from the surl executable

## Goal

`surl gs://127.0.0.1:0` starts the Google Cloud Storage server, with TLS as BL-429's ADR decides. Its
`--aihelp` topic and help category exist, and the extension switch turns it off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): a `HelpCategories` row with `gs`, an
  `AiHelpProse.TopicAbout` paragraph naming the XML API as its oracle, an `AiHelpExamples` entry, and the topic
  list in `AiHelpTextTests` growing by one.
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 1): `gs` is handed the **same** `ContentStore`
  instance every original file server gets (buckets resolve through BL-655's lookup), and the same account set. If
  BL-609 has moved the composition to `Application/Surl.Composition.UnitLibrary` (`ExtendedProtocolServers.Compose`
  keeps its name), register it there and record the changed touches in Notes.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test finds `gs` registered when the switch is on, and absent when it is off.
- [ ] A named test shows the `gs` server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-655; composed with the one shared content store.
