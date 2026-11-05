---
id: BL-421
title: Serve s3 from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-420, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-421 — Serve s3 from the surl executable

## Goal

`surl s3://127.0.0.1:0` starts the S3 server with the content store, accounts, `--allow-uploads`,
`--allow-anonymous` and TLS as BL-417's ADR decides. Its `--aihelp` topic and help category exist, and the
extension switch turns it off.

## Context

- Composition: the extended-server composition from BL-405 in `Application/Surl.Console/CommandLineRunner.cs`,
  with the default port from the extended port table. If the ADR serves `s3` over TLS, wrap the server as the
  original `https` is wrapped (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): a `HelpCategories` row with its scheme, an
  `AiHelpProse.TopicAbout` paragraph saying the server is judged by the S3 API Reference and not by upstream
  curl, an `AiHelpExamples` entry, and the topic list pinned in `AiHelpTextTests` growing by one.
- Any new option needs its `OptionArgumentType` and its categories (ADR-0034).

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test composes the servers and finds `s3` registered when the switch is on,
      and absent when it is off.
- [ ] `CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic`,
      `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder`,
      `AiHelpTextTests.Answer_EveryTopicAndTheOverview_HasItsAboutText`,
      `AiHelpTextTests.Answer_EveryTopicButExitCodesAndSecurity_HasAnExample` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
