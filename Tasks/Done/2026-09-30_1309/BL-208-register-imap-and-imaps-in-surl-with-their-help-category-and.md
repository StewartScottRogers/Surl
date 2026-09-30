---
id: BL-208
title: Register imap and imaps in surl with their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-207, BL-203, BL-204]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests, Surl.Protocol.Imap.UnitLibrary]
requirement: FR-044
created: 2026-09-29
completed: 2026-09-30
---
# BL-208 — Register imap and imaps in surl with their help category and --aihelp topic

## Goal

`surl imap://127.0.0.1:<port>/` and `surl imaps://...` serve the same mail store BL-207 composes
with `ImapProtocolServer`, and `surl --help` and `--aihelp` list the `imap` category and topic.

## Context

- Decisions: BL-187's ADR (the category name, how `imaps` is claimed); ADR-0034 decision 1 and
  ADR-0046 decision 3.
- Code: `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` (pass BL-207's store
  instance, so mail delivered over SMTP is read over IMAP in the same run; the SASL policy);
  `ImplicitTlsSchemeServer` if BL-204 did not claim `imaps`. `Surl.Cli.UnitLibrary/HelpCategories.cs`,
  `CommandLineOptions.cs` (categories of every option the IMAP server reads), `AiHelpProse.cs`,
  `AiHelpExamples.cs`, `ManualText.cs`.
- Keep this task to wiring; a server defect becomes a `Surl.Protocol.Imap` task.

## Acceptance criteria

- [x] A fast `CommandLineRunnerTests` test shows `imap://` and `imaps://` listen URLs start
      listeners with the IMAP server, sharing the SMTP server's store instance; `--version`'s
      `Protocols:` line lists `imap` and `imaps`.
- [x] `surl --help category` lists `imap`; `--help imap` lists every option the server reads;
      `--aihelp imap` answers with its `About` and example; `CommandLineRunnerAiHelpTests`,
      `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests` pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Wiring: `ComposeProtocolServers` builds one `ImapProtocolServer` (the policy as both policies,
  the same `MailboxStore` instance as `SmtpProtocolServer`, `isTlsUpgradeAvailable`) and wraps it
  in `ImplicitTlsSchemeServer` for `imaps` (ADR-0055 decision 11). `ServerTlsComposition`'s
  `UpgradableSchemes` gains `imap`, so `--self-signed` with an `imap://` URL makes the certificate
  STARTTLS needs.
- Help: the `imap` category (`IMAP and IMAPS protocol`, schemes `imap`, `imaps`) holds exactly
  ADR-0055 decision 14's options; the 37 exit-code row gains `imap` (the mail store). The texts that
  said "of those three protocols this build serves only SMTP yet" now say "SMTP and IMAP, not POP3
  yet"; `--allow-anonymous`, `--allow-plaintext-auth`, the auth and content topics and the manual
  name IMAP where they named SMTP. `--keytab` stays in `auth` alone, as it does for SMTP.
- Defaults taken: the acceptance's `CommandLineRunnerTests` test lives in a new
  `CommandLineRunnerImapTests` class beside `CommandLineRunnerSmtpTests`, as each protocol's wiring
  tests do. The shared-store test needed two connections in order, so `FakeListenerFactory` gained
  `ConnectionsByScheme` (one task per scheme, awaited by that listener's first accept); SMTP
  delivers under `--allow-anonymous`, then IMAP `SELECT`s and `FETCH`es the message. The `--aihelp
  imap` example is `--allow-anonymous imap://127.0.0.1:0/` with `curl imap://127.0.0.1:<port>/`,
  the SMTP example's shape. No new ADR: every choice follows ADR-0055 decisions 11 and 14.
- `touches` gained `Surl.Protocol.Imap.UnitLibrary` for its `CLAUDE.md` only, which said the server
  was not yet registered (BL-208); no task in Doing names it.
- Measured: `Surl.Cli.UnitLibrary` and `Surl.Console` 100% line and branch, no failing member;
  7268 fast tests pass.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl serves imap:// and imaps:// from the mail store SMTP delivers into, with the imap help category and --aihelp topic
