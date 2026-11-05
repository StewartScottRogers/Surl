---
id: BL-522
title: Serve kafka and kafkas from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-521, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-522 — Serve kafka and kafkas from the surl executable

## Goal

`surl kafka://127.0.0.1:0` and `surl kafkas://127.0.0.1:0` (TLS) start the Kafka broker with its log, which is
loaded after the data-directory lock if it persists. Metadata advertises the listener's own host and port. The
`--aihelp` topic and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `kafkas` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010). Metadata's broker host and port come from
  the bound listener, so a client reconnecting to the advertised address reaches the same `surl`.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming the Kafka protocol guide as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `kafka` and `kafkas` registered when the switch is on, and absent when it is off.
      Another shows the advertised broker port equal to the bound port when the listen URL says `:0`.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
