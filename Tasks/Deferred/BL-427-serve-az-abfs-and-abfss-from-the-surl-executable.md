---
id: BL-427
title: Serve az, abfs and abfss from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-426, BL-405, BL-655]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
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
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 1): the Azure server is handed the **same**
  `ContentStore` instance every original file server gets (containers resolve through BL-655's lookup), and the
  same account set. If BL-609 has moved the composition to `Application/Surl.Composition.UnitLibrary`
  (`ExtendedProtocolServers.Compose` keeps its name), register it there and record the changed touches in Notes.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test finds `az`, `abfs` and `abfss` registered when the switch is on, and
      absent when it is off.
- [ ] A named test shows the Azure server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, including
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- BL-423 decided (ADR-0109 decision 6): `az` and `abfss` implicit TLS, `abfs` plain HTTP; this task also adds
  `--az-plaintext` (no argument, category `azure-storage`, warned on start) and refuses a bearer token over plain
  text unless `--allow-plaintext-auth`. The `--aihelp` topic is `azure-storage` (decision 8).
- 2026-11-05: BL-609 has moved the composition: extended servers register in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`, beside `s3`), and the
  shared-store tests belong in `Surl.Composition.UnitTests`. Both are added to `touches`. BL-803 (in Doing) touches
  the same two projects, so this task waits in Backlog until BL-803 finishes.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-655; composed with the one shared content store.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests, which BL-803 (Doing) touches; resume once BL-803 is Done
- 2026-11-06: Backlog -> Deferred. Split into BL-965, BL-966 and BL-967 (over the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06)
