---
id: BL-317
title: Register rtsp in surl with its help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-316]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-051
created: 2026-09-30
completed:
---
# BL-317 — Register rtsp in surl with its help category and --aihelp topic

## Goal

`surl rtsp://127.0.0.1:<port>/` serves RTSP with `RtspProtocolServer`, its challenges answered by
`Surl.Authentication`'s HTTP authentication session, and `surl --help` and `--aihelp` list the RTSP
category and topic.

## Context

- Decisions: BL-286's ADR (the category and topic names, the options the server reads); ADR-0034
  decision 1 and ADR-0046 decision 3 (category row, option categories, topic `About` and example in
  the same change). curl has no `rtsps`, so there is no TLS scheme to register (ADR-0026 decision 3).
- Code: `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` (register the server with the same
  `IHttpAuthenticationSession` factory the HTTP server gets), `Surl.Console/AuthenticationComposition.cs`;
  `Surl.Cli.UnitLibrary/HelpCategories.cs`, `CommandLineOptions.cs` (categories of the options the
  server reads, e.g. `--max-request-head` whose explanation already names RTSP, `--head-timeout`,
  `--max-filesize`, `--allow-uploads`, `--user`, `--user-file`, `--allow-anonymous`,
  `--allow-plaintext-auth`, `--auth`, `--directory`, as the ADR lists), `AiHelpProse.cs`,
  `AiHelpExamples.cs`, `ManualText.cs`, and `VersionText.cs`'s `Protocols:` line.
- Root `CLAUDE.md`'s `--aihelp` completeness rule: the topic list pinned in
  `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder` grows by one, and
  `CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic` passes.
- Keep this task to wiring; a server defect becomes a `Surl.Protocol.Rtsp` task.

## Acceptance criteria

- [ ] A fast `CommandLineRunnerTests` test shows an `rtsp://` listen URL starts a listener with the
      RTSP server (through `FakeListenerFactory`); `--version`'s `Protocols:` line lists `rtsp`.
- [ ] `surl --help category` lists the RTSP category; `--help <category>` lists every option the
      server reads; `--aihelp <topic>` answers with its `About` and example; `AiHelpTextTests`,
      `AiHelpFactsTests`, `CommandLineRunnerAiHelpTests`, `HelpTextTests` and `ManualTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
