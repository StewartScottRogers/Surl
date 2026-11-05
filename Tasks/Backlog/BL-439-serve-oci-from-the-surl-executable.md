---
id: BL-439
title: Serve oci from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-438, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-439 — Serve oci from the surl executable

## Goal

`surl oci://127.0.0.1:0` starts the OCI registry with its storage, token endpoint and TLS as BL-435's ADR
decides. Its `--aihelp` topic and help category exist, and the extension switch turns it off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`. If
  the registry keeps state across connections, load it after the data-directory lock, as the mail store is loaded
  (`ServiceState`).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): a `HelpCategories` row with `oci`, an
  `AiHelpProse.TopicAbout` paragraph naming the distribution spec as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test finds `oci` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
