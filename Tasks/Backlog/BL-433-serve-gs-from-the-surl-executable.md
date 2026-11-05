---
id: BL-433
title: Serve gs from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-432, BL-405]
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

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test finds `gs` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
