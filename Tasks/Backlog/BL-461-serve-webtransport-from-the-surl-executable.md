---
id: BL-461
title: Serve webtransport from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-460, BL-405, BL-384]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-461 — Serve webtransport from the surl executable

## Goal

`surl webtransport://127.0.0.1:0` listens on UDP with QUIC and HTTP/3, as BL-384 does for `https`, and serves
WebTransport sessions. The `--aihelp` topic and help category exist, and the extension switch turns it off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`, using
  the QUIC listener and the certificate composition BL-384 added for HTTP/3.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with `webtransport`, an
  `AiHelpProse.TopicAbout` paragraph naming the pinned WTH3 draft as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `webtransport` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
