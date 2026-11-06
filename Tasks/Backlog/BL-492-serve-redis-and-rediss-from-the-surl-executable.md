---
id: BL-492
title: Serve redis and rediss from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-491, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-492 — Serve redis and rediss from the surl executable

## Goal

`surl redis://127.0.0.1:0` and `surl rediss://127.0.0.1:0` (TLS) start the Redis server, with its keyspace loaded
after the data-directory lock if it persists. The `--aihelp` topic and help category exist, and the extension
switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `rediss` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010). State kept across connections is loaded as
  `ServiceState` loads the mail store.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming the RESP specification as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `redis` and `rediss` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 4): extended servers are composed in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`), not in
  `CommandLineRunner.cs` - the composition moved there (BL-804), and BL-421 registered `s3` the same way,
  touching Surl.Composition and its tests. Redis needs a `Registrations` entry, a project reference from
  Surl.Composition to `Surl.Extended.Protocol.Redis.UnitLibrary`, its keyspace loaded in `ServiceState`, and a
  test in `ExtendedProtocolServersTests`. So `touches` now names Surl.Composition.UnitLibrary and .UnitTests;
  BL-803 (in Doing) holds them, so this task went back to Backlog until they no longer overlap.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests (where extended servers are registered), which BL-803 in Doing touches
