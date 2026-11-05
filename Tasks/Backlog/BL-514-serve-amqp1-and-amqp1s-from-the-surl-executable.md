---
id: BL-514
title: Serve amqp1 and amqp1s from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-513, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-514 — Serve amqp1 and amqp1s from the surl executable

## Goal

`surl amqp1://127.0.0.1:0` and `surl amqp1s://127.0.0.1:0` (TLS) start the AMQP 1.0 container with its shared
nodes. The `--aihelp` topic and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `amqp1s` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming OASIS AMQP 1.0 as its oracle, an `AiHelpExamples` entry, and the topic
  list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `amqp1` and `amqp1s` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
