---
id: BL-303
title: Register ws and wss in surl with their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-302]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-048
created: 2026-09-30
completed:
---
# BL-303 — Register ws and wss in surl with their help category and --aihelp topic

## Goal

`surl ws://127.0.0.1:<port>/` and `surl wss://...` serve WebSocket with `WsProtocolServer`, its
upgrade challenges answered by `Surl.Authentication`'s HTTP authentication session, and `surl --help`
and `--aihelp` list the WebSocket category and topic.

## Context

- Decisions: BL-285's ADR (the category and topic names, how `wss` is claimed, any option the server
  reads); ADR-0010 (`wss` is TLS from the first byte, ALPN `http/1.1`, a certificate from `--cert` or
  `--self-signed`); ADR-0034 decision 1 and ADR-0046 decision 3 (category row, option categories,
  topic `About` and example in the same change).
- Code: `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` (register the server, with
  `ImplicitTlsSchemeServer` for `wss` as for `smtps`, and the same `IHttpAuthenticationSession`
  factory the HTTP server gets), `Surl.Console/AuthenticationComposition.cs`;
  `Surl.Cli.UnitLibrary/HelpCategories.cs`, `CommandLineOptions.cs` (categories of the options the
  server reads: `--max-request-head`, `--head-timeout`, `--max-message`, `--user`, `--user-file`,
  `--allow-anonymous`, `--allow-plaintext-auth`, `--auth`, `--directory`, as the ADR lists),
  `AiHelpProse.cs`, `AiHelpExamples.cs`, `ManualText.cs`, and `VersionText.cs`'s `Protocols:` line.
- Root `CLAUDE.md`'s `--aihelp` completeness rule: the topic list pinned in
  `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder` grows by one, and
  `CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic` passes.
- Keep this task to wiring; a server defect becomes a `Surl.Protocol.Ws` task.

## Acceptance criteria

- [ ] A fast `CommandLineRunnerTests` test shows `ws://` and `wss://` listen URLs start listeners
      with the WebSocket server (through `FakeListenerFactory`), `wss://` needing `--cert` or
      `--self-signed`; `--version`'s `Protocols:` line lists `ws` and `wss`.
- [ ] `surl --help category` lists the WebSocket category; `--help <category>` lists every option
      the server reads; `--aihelp <topic>` answers with its `About` and example; `AiHelpTextTests`,
      `AiHelpFactsTests`, `CommandLineRunnerAiHelpTests`, `HelpTextTests` and `ManualTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
