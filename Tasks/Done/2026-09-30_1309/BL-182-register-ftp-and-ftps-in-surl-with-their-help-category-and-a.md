---
id: BL-182
title: Register ftp and ftps in surl with their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-175, BL-176, BL-179, BL-180, BL-181]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-036
created: 2026-09-29
completed: 2026-09-30
---
# BL-182 — Register ftp and ftps in surl with their help category and --aihelp topic

## Goal

`surl ftp://127.0.0.1:<port>/` and `surl ftps://...` serve the content store with
`FtpProtocolServer`, real data connections from `Surl.Networking` passed through the serving
engine, and `surl --help` and `--aihelp` list the `ftp` category and topic.

## Context

- Decisions: BL-173's ADR (the category name and description, how `ftps` is claimed); ADR-0034
  decision 1 (the registering task adds the category row and adds it to every option the server
  reads); ADR-0046 decision 3 (the topic's `About` and example in the same change).
- Code: `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` (the FTP server with the
  `ContentStore` and the authentication policy; `ImplicitTlsSchemeServer` for `ftps` if BL-181
  did not claim it) and the `ServingEngine` construction (pass BL-175's opener into BL-176's
  parameter). `Surl.Cli.UnitLibrary/HelpCategories.cs`, `CommandLineOptions.cs` (categories of
  `--allow-uploads`, `--list-directories`, `--follow-symlinks`, `--serve-dot-files`,
  `--max-line`, `--head-timeout`, `--max-filesize`, `--user`, `--user-file`,
  `--allow-anonymous`, `--allow-plaintext-auth`, and any FTP option the ADR adds),
  `AiHelpProse.cs`, `AiHelpExamples.cs`, `ManualText.cs`.
- Keep this task to wiring; a server defect becomes a `Surl.Protocol.Ftp` task.

## Acceptance criteria

- [x] A fast `CommandLineRunnerTests` test shows `ftp://` and `ftps://` listen URLs start
      listeners with the FTP server (through `FakeListenerFactory`), `ftps://` needing `--cert` or
      `--self-signed` as ADR-0032 section 10 says; `--version`'s `Protocols:` line lists `ftp`
      and `ftps`.
- [x] `surl --help category` lists `ftp`; `--help ftp` lists every option the server reads;
      `--aihelp ftp` answers with its `About` and example; `CommandLineRunnerAiHelpTests`,
      `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests` pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Wiring, as ADR-0052 decisions 5, 9 and 11 already decide it (no new ADR needed):
  `ComposeProtocolServers` registers `FtpProtocolServer(contentStore, policy, isTlsUpgradeAvailable)`,
  which claims `ftp` and `ftps` itself (BL-181), so no `ImplicitTlsSchemeServer` wraps it.
  `ServerTlsComposition.UpgradableSchemes` gains `ftp`, so `--cert`/`--self-signed` with an `ftp://`
  URL makes TLS settings and `AUTH TLS` is offered; without a certificate `AUTH` is `534`. The
  `ServingEngine` now gets `SocketDataConnectionOpener(tls.Settings, timeProvider)` in place of
  `RefusingDataConnectionOpener`, through a new optional `createDataConnectionOpener` runner seam
  (tests pass `InMemoryDataConnections`; `surl` passes nothing). The default lives in its own
  method because a third `??` in the primary constructor pushed its complexity to 12.
- Choices taken: `ComposeProtocolServers`' `isStartTlsAvailable` is renamed `isTlsUpgradeAvailable`,
  since it now also drives FTP's `AUTH TLS`. The `listen-urls` "scheme this build does not serve"
  example moves from `ftp` to `rtsp`, the scheme the Console tests already use for it (imap and pop3
  have registration tasks queued). The `ftp` category holds exactly ADR-0052 decision 11's twelve
  options; `--allow-anonymous` and `--allow-plaintext-auth` explanations, the auth, content and tls
  `--aihelp` prose and the manual's ACCOUNTS paragraph now name FTP.
- The earlier run's hang (lane 6) did not reproduce: `Surl.Console.UnitTests` passed 309/309 in
  about 1 s on six consecutive runs with FTP registered, and the whole fast suite passed.
- Measured: `Measure-CodeQuality.ps1` - `Surl.Cli.UnitLibrary` and `Surl.Console` 100% line and
  branch, 0 failing members; 0 failing across all 30 production assemblies.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Blocked. Stewart: Surl dark factory timed out after 120 min; see Z:\repos\Surl.logs\BL-182-20260930-070942-L6.jsonl
- 2026-09-30: Blocked -> Backlog. Requeued by Claude: not a question for Stewart. The run timed out after 120 min chasing one Surl.Console.UnitTests test that hangs only under parallel load once FtpProtocolServer is registered in CommandLineRunner (each test class passes alone, 281; the full run finishes 280). Find that test first: run the full class set with detailed logging and diff the names; its lane-6 work was not kept.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl serves ftp:// and ftps:// with FtpProtocolServer over real data connections, AUTH TLS with a certificate, and --help/--aihelp list the ftp category and topic
