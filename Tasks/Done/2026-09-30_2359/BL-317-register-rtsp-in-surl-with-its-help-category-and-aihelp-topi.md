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
completed: 2026-09-30
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

- [x] A fast `CommandLineRunnerTests` test shows an `rtsp://` listen URL starts a listener with the
      RTSP server (through `FakeListenerFactory`); `--version`'s `Protocols:` line lists `rtsp`.
- [x] `surl --help category` lists the RTSP category; `--help <category>` lists every option the
      server reads; `--aihelp <topic>` answers with its `About` and example; `AiHelpTextTests`,
      `AiHelpFactsTests`, `CommandLineRunnerAiHelpTests`, `HelpTextTests` and `ManualTextTests` pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Delivered the way BL-303 registered WebSocket: no plan beyond ADR-0074 decision 9, which names
  the category, its options and the topic's content. `RtspProtocolServer` is composed in
  `ComposeProtocolServers` with the one content store and the one `AuthenticationPolicy` (its
  two-argument constructor, the system random source); no `ImplicitTlsSchemeServer`, since curl
  has no `rtsps`. No new option, so `SurlCommandLine` is unchanged.
- `rtsp` added to the categories of `--directory`, `--allow-uploads`, `--head-timeout`,
  `--max-request-head`, `--max-filesize`, `--user`, `--user-file`, `--allow-anonymous`,
  `--allow-plaintext-auth` and `--auth`, exactly the ADR's list; the content topic's two
  paragraphs and the manual's `--max-request-head` line now name RTSP, and the manual has an
  `RTSP OPTIONS` section.
- Choice: the `listen-urls` example and the runner tests that used `rtsp` as "a scheme this build
  does not serve" now use `ldap`, the only scheme `SchemeDefaultPorts` knows that is still
  unregistered. Why: it keeps the example runnable and true; whichever task registers LDAP must
  pick another unserved scheme (a scheme surl does not know is refused the same way).
- New tests: `CommandLineRunnerRtspTests` (pinned curl 8.21.0's `OPTIONS *` answered 200 with all
  ten methods in `Public`; with `-u` the same request is challenged 401 Digest, proving the policy
  is wired) and `HelpTextTests.Answer_Rtsp_ListsEveryOptionTheRtspServerReads`.
- Measured: Surl.Cli.UnitLibrary and Surl.Console 100% line and branch, 0 failing members, worst
  CRAP 10. Fast tests all green (Cli 710, Console 386).
- Product docs for RTSP stay with BL-319.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl rtsp:// serves RTSP through the HTTP authentication policy; --help rtsp, --aihelp rtsp, the manual and --version list it
