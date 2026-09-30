---
id: BL-209
title: Register pop3 and pop3s in surl with their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-207, BL-206]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests, Surl.Protocol.Pop3.UnitLibrary]
requirement: FR-045
created: 2026-09-29
completed: 2026-09-30
---
# BL-209 — Register pop3 and pop3s in surl with their help category and --aihelp topic

## Goal

`surl pop3://127.0.0.1:<port>/` and `surl pop3s://...` serve the same mail store BL-207 composes
with `Pop3ProtocolServer`, and `surl --help` and `--aihelp` list the `pop3` category and topic.

## Context

- Decisions: BL-188's ADR (the category name, how `pop3s` is claimed); ADR-0034 decision 1 and
  ADR-0046 decision 3.
- Code: `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` (pass BL-207's store
  instance; the SASL policy); `ImplicitTlsSchemeServer` if BL-206 did not claim `pop3s`.
  `Surl.Cli.UnitLibrary/HelpCategories.cs`, `CommandLineOptions.cs` (categories of every option
  the POP3 server reads), `AiHelpProse.cs`, `AiHelpExamples.cs`, `ManualText.cs`.
- Keep this task to wiring; a server defect becomes a `Surl.Protocol.Pop3` task.

## Acceptance criteria

- [x] A fast `CommandLineRunnerTests` test shows `pop3://` and `pop3s://` listen URLs start
      listeners with the POP3 server, sharing the SMTP server's store instance; `--version`'s
      `Protocols:` line lists `pop3` and `pop3s`.
- [x] `surl --help category` lists `pop3`; `--help pop3` lists every option the server reads;
      `--aihelp pop3` answers with its `About` and example; `CommandLineRunnerAiHelpTests`,
      `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests` pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Wiring: `ComposeProtocolServers` builds one `Pop3ProtocolServer` (the policy as both policies,
  the same `MailboxStore` instance as the SMTP and IMAP servers, `isTlsUpgradeAvailable` as
  `isStlsAvailable`) and wraps it in `ImplicitTlsSchemeServer` for `pop3s` (ADR-0056 decision 8).
  `ServerTlsComposition`'s `UpgradableSchemes` gains `pop3`, so `--self-signed` with a `pop3://`
  URL makes the certificate STLS needs.
- Help: the `pop3` category (`POP3 and POP3S protocol`, schemes `pop3`, `pop3s`) holds exactly
  ADR-0056 decision 11's options (`--max-filesize` left out: POP3 takes no upload); the 37 exit-code
  row gains `pop3` (the mail store). The texts that said "this build serves SMTP and IMAP, not POP3
  yet" drop that clause; `--allow-anonymous`, `--allow-plaintext-auth`, the auth, content and tls
  topics and the manual name POP3 beside SMTP and IMAP.
- Defaults taken: the wiring tests live in a new `CommandLineRunnerPop3Tests` beside
  `CommandLineRunnerImapTests`, as each protocol's do; the shared-store test delivers over SMTP
  under `--allow-anonymous`, then `STAT`s and `RETR`s over POP3 through
  `FakeListenerFactory.ConnectionsByScheme`. The `--aihelp pop3` example is `--allow-anonymous
  pop3://127.0.0.1:0/` with `curl pop3://127.0.0.1:<port>/`, the IMAP and SMTP examples' shape.
  No new ADR: every choice follows ADR-0056 decisions 8 and 11.
- `touches` gained `Surl.Protocol.Pop3.UnitLibrary` for its `CLAUDE.md` only, which said the
  composition was not yet built (BL-209); no task in Doing names it.
- Measured: `Surl.Cli.UnitLibrary`, `Surl.Console` and `Surl.Protocol.Pop3.UnitLibrary` 100% line
  and branch, no failing member (0 across 30 assemblies); 7284 fast tests pass.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl serves pop3:// and pop3s:// from the mail store SMTP delivers into, with the pop3 help category and --aihelp topic
