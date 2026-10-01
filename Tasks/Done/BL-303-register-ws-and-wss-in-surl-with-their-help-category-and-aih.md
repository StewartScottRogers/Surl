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
completed: 2026-09-30
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

- [x] A fast `CommandLineRunnerTests` test shows `ws://` and `wss://` listen URLs start listeners
      with the WebSocket server (through `FakeListenerFactory`), `wss://` needing `--cert` or
      `--self-signed`; `--version`'s `Protocols:` line lists `ws` and `wss`.
- [x] `surl --help category` lists the WebSocket category; `--help <category>` lists every option
      the server reads; `--aihelp <topic>` answers with its `About` and example; `AiHelpTextTests`,
      `AiHelpFactsTests`, `CommandLineRunnerAiHelpTests`, `HelpTextTests` and `ManualTextTests` pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Delivered directly as wiring, the way ADR-0071 decision 9 decides it: no plan stage was
  needed, because the ADR already names the category, the topic, the schemes, the options and
  `--ws-echo`.
- `ComposeProtocolServers` registers one `WsProtocolServer` (the one content store, the one
  `AuthenticationPolicy`, so its upgrades get the HTTP authentication session) for `ws`, and the
  same server through `ImplicitTlsSchemeServer` for `wss`. It takes a new
  `echoesWebSocketMessages` argument, `SurlCommandLine.WsEcho`, which is false for the servers
  composed only for their schemes.
- `--ws-echo` (negatable flag, category `websocket`, default off) is new in
  `CommandLineOptions`. Its description is `Echo client messages, not the path` rather than the
  ADR's `Echo every client message instead of serving the path`: `HelpLayout` narrows the
  `--help all` description column for every option once one description reaches 39 characters,
  so the ADR's 52 characters would have moved the whole list off column 38. ADR-0071 could not
  be edited here, because BL-286 holds `Documentation/Planning/Decisions`. BL-330 is filed to
  align the ADR.
- The category `websocket` (`WebSocket protocol`, schemes `ws` and `wss`) was added to
  `--max-request-head`, `--head-timeout`, `--max-message`, `--user`, `--user-file`,
  `--allow-anonymous`, `--allow-plaintext-auth`, `--auth`, `--directory` and
  `--list-directories`, as ADR-0071 decision 9 lists. The category name is one character longer
  than `security`, so `--help category`'s description column moved by one.
- `--aihelp websocket` has six `About` paragraphs and two examples, serving and `--ws-echo`. The
  `tls` and `content` paragraphs now name `wss` and WebSocket. The manual gains a `WEBSOCKET
  OPTIONS` section, and its `--max-request-head` and `--max-message` limit lines now name
  WebSocket.
- The runner tests are in a new `CommandLineRunnerWsTests` class beside `CommandLineRunnerSmtpTests`
  and the others, one class per protocol. The `--version` test in `CommandLineRunnerTests` is
  renamed `RunAsync_Version_WritesVersionWithEveryRegisteredSchemeAndReturnsOk`, since it now pins
  every scheme.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl serves ws:// and wss:// with WsProtocolServer (--ws-echo to echo), and --help/--aihelp/--manual/--version list WebSocket
