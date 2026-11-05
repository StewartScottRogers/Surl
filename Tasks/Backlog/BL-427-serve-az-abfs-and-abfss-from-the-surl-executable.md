---
id: BL-427
title: Serve az, abfs and abfss from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-426, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-427 — Serve az, abfs and abfss from the surl executable

## Goal

`surl az://`, `surl abfs://` and `surl abfss://` start the Azure Storage server with TLS where BL-423's ADR
decides. Its `--aihelp` topic and help category exist, and the extension switch turns it off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `abfss` is wrapped for TLS as `https` is (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): a `HelpCategories` row with the three schemes, an
  `AiHelpProse.TopicAbout` paragraph saying the server is judged by the Azure Storage REST API and not by
  upstream curl, an `AiHelpExamples` entry, and the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test finds `az`, `abfs` and `abfss` registered when the switch is on, and
      absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, including
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
