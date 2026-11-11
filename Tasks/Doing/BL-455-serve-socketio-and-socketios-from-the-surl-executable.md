---
id: BL-455
title: Serve socketio and socketios from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-454, BL-405]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-455 — Serve socketio and socketios from the surl executable

## Goal

`surl socketio://127.0.0.1:0` and `surl socketios://127.0.0.1:0` (TLS) start the Socket.IO server. The `--aihelp`
topic and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `socketios` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming the Engine.IO v4 and Socket.IO v5 specifications as its oracle, an
  `AiHelpExamples` entry, and the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `socketio` and `socketios` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 4): Extended servers are registered in `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`, ADR-0124), not in `Surl.Console`'s `CommandLineRunner` as Context says; the s3 precedent (BL-421, commit efa2f1bc) changed `ExtendedProtocolServers`, `ExtendedProtocolServerServices` and `Surl.Composition.UnitTests`. Added both Composition projects to `touches`. BL-803 (in Doing) touches them, so the task returns to Backlog until BL-803 frees them. `socketios` should join `TlsSchemes` the way `s3` does rather than a second registration.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests (extended server registration), which BL-803 in Doing holds
- 2026-11-11: Backlog -> Doing.
