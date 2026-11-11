---
id: BL-1023
title: Serve az, abfs and abfss from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-426, BL-405, BL-655]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1023 — Serve az, abfs and abfss from the surl executable

## Goal

`surl az://`, `surl abfs://` and `surl abfss://` start the Azure Storage server - `az` and `abfss` over TLS, `abfs`
as plain HTTP/1.1 (ADR-0109 decision 6) - on the shared content store, its `--aihelp` topic and help category
exist, and the extension switch turns it off.

## Context

- First of the three tasks BL-427 ("Serve az, abfs and abfss from the surl executable") was split into on
  2026-11-06 for the 60-minute size limit: this, BL-1024 (the plain-text bearer refusal) and BL-1025
  (`--az-plaintext`).
- Composition: extended servers register in `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs`
  (`Registrations`, beside `s3`, as BL-421 did), and the shared-store tests belong in `Surl.Composition.UnitTests`.
  `az` and `abfss` are wrapped for TLS as `https` is (`ImplicitTlsSchemeServer`, ADR-0010), `--cert` or
  `--self-signed` required, ALPN `http/1.1` only; `abfs` is plain on port 80.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): the topic is `azure-storage` (ADR-0109 decision 8):
  a `HelpCategories` row with the three schemes, an `AiHelpProse.TopicAbout` paragraph saying the server is judged
  by the Azure Storage REST API and not by upstream curl, an `AiHelpExamples` entry, and the topic list in
  `AiHelpTextTests` growing by one. The paragraph states what is true when this task lands; BL-1025 adds the
  `--az-plaintext` sentence.
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 1): the Azure server is handed the **same**
  `ContentStore` instance every original file server gets (containers resolve through BL-655's lookup), and the
  same account set.
- Stand-in: the shared content store is the store; a test seeds it through `Surl.Api`'s file seeding and
  observes the content store's write events.

## Acceptance criteria

- [ ] A named test finds `az`, `abfs` and `abfss` registered when the switch is on, and absent when it is off.
- [ ] A named test shows the Azure server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, including
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
