---
id: BL-450
title: Serve signalr and signalrs from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-449, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-450 — Serve signalr and signalrs from the surl executable

## Goal

`surl signalr://127.0.0.1:0` and `surl signalrs://127.0.0.1:0` (TLS) start the SignalR hub. The `--aihelp` topic
and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `signalrs` is wrapped for TLS as `wss` is (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming HubProtocol.md and TransportProtocols.md as its oracle, an
  `AiHelpExamples` entry, and the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `signalr` and `signalrs` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
