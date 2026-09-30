---
id: BL-207
title: Register smtp and smtps in surl with the mail store, their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-191, BL-200, BL-197]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-043
created: 2026-09-29
completed: 2026-09-30
---
# BL-207 — Register smtp and smtps in surl with the mail store, their help category and --aihelp topic

## Goal

`surl smtp://127.0.0.1:<port>/` and `surl smtps://...` receive mail with `SmtpProtocolServer`
into one mail store per `surl` run - in memory, or under `<path>/.surl/mail` with `--directory`,
loaded at start - and `surl --help` and `--aihelp` list the `smtp` category and topic.

## Context

- Decisions: BL-184's ADR (the store's composition and persistence, the malformed-store
  refusal); BL-186's ADR (the category name, how `smtps` is claimed); ADR-0031 decision 7 (the
  start-up order: probe, scheme check, lock, load service state, bind - load the store after the
  lock, a malformed store `CouldNotReadFile` (37) with the ADR's text before any listener binds);
  ADR-0034 decision 1 and ADR-0046 decision 3 (category row, option categories, topic `About`
  and example in the same change).
- Code: `Surl.Console/CommandLineRunner.cs` - `ComposeRetainedMessageFile` and
  `LoadRetainedMessagesAsync` are the MQTT pattern to follow; `ComposeProtocolServers` (one
  store instance, later shared with IMAP and POP3 by BL-208 and BL-209);
  `AuthenticationComposition.cs` (the SASL policy); `ImplicitTlsSchemeServer` if BL-199 did not
  claim `smtps`. `Surl.Cli.UnitLibrary/HelpCategories.cs`, `CommandLineOptions.cs` (categories of
  `--max-line`, `--head-timeout`, `--max-filesize`, `--user`, `--user-file`, `--allow-anonymous`,
  `--allow-plaintext-auth`, `--auth`, `--directory` as the server reads them), `AiHelpProse.cs`,
  `AiHelpExamples.cs`, `ManualText.cs` (the manual's data-directory section gains `.surl/mail`).
- Keep this task to wiring; a server defect becomes a `Surl.Protocol.Smtp` task.

## Acceptance criteria

- [x] A fast `CommandLineRunnerTests` test shows `smtp://` and `smtps://` listen URLs start
      listeners with the SMTP server (through `FakeListenerFactory`), `smtps://` needing `--cert`
      or `--self-signed`; `--version`'s `Protocols:` line lists `smtp` and `smtps`.
- [x] Fast tests show the store loaded from `<path>/.surl/mail` with `--directory`, in memory
      without it, and a malformed store refused with 37 and the ADR's text before any listener
      binds.
- [x] `surl --help category` lists `smtp`; `--help smtp` lists every option the server reads;
      `--aihelp smtp` answers with its `About` and example; `CommandLineRunnerAiHelpTests`,
      `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests` pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Plan, as built: `CommandLineRunner.ComposeProtocolServers` registers one `SmtpProtocolServer`
  for `smtp` and, through `ImplicitTlsSchemeServer`, `smtps` (ADR-0053 decision 5), given the
  `AuthenticationPolicy` as both its `IAuthenticationPolicy` and `IMailAuthenticationPolicy`.
  After the lock, `LoadServiceStateAsync` loads the MQTT retained messages, then the mail store
  (`ComposeMailStoreFiles`: `<full path>/.surl/mail` with `--directory`, none without;
  `LoadMailStoreAsync`: `MailboxStore.LoadAsync` or the in-memory constructor). A
  `MailStoreLoadException` gives `surl: (37) Could not read <FilePath>: <Message>` (ADR-0050
  decision 7) before any listener binds. The two are carried as a private `ServiceState` record.
- Choice: the store's owners are the account names, so `AuthenticationComposition.Compose` now
  returns them beside the policy (`AccountBook` exposes no names, and `Surl.Authentication` is
  outside `touches`). One message is bounded by `--max-filesize` (`Limits.MaxUploadBytes`),
  ADR-0050 decision 6's "as each server reads it"; the other store bounds keep their defaults.
- Choice: `STARTTLS` is offered when `--cert` or `--self-signed` is given
  (`ServerTlsComposition.IsCertificateConfigured`), and `ServerTlsComposition.Compose` now makes
  the TLS settings - the certificate the upgrade needs - for an `smtp` listen URL when either is
  given, not only for a scheme TLS from the first byte. ADR-0053 decision 5 already decides this
  ("`Surl.Console` sets when a server certificate is configured"; ADR-0010 makes the throwaway
  certificate for a scheme that can upgrade), so no new ADR. So `--self-signed smtp://...` now
  writes the `--self-signed` warning, which is true: it serves a throwaway certificate.
- Texts made true now SMTP is served: the `--auth` explanation, manual and `auth` topic said "this
  build serves none of those three protocols yet" and now say only SMTP is served; the
  `--allow-anonymous` and `--allow-plaintext-auth` explanations name SMTP; the manual's ACCOUNTS
  and DATA DIRECTORY sections, the `content` and `tls` topics and the 37 exit-code row name the
  mail store and `smtps`. The `smtp` category holds `--allow-anonymous`, `--allow-plaintext-auth`,
  `--auth`, `--directory`, `--head-timeout`, `--max-filesize`, `--max-line`, `--user` and
  `--user-file` (ADR-0053 decision 9).
- Renamed, since their names carried counts that grew: `AiHelpExamplesTests.Examples_AreTheAdrsNineteenAndSmtpsInItsOrder`
  and `AiHelpFactsTests.OnlyTheSevenProtocolCategories_HaveSchemes`.
  `AiHelpTextTests.Topics_AreTheAdrsSixteenInOrdinalOrder` keeps its name (the root `CLAUDE.md`
  names it, outside `touches`); BL-244 renames all three without a count.
- Tests: `Surl.Console.UnitTests/CommandLineRunnerSmtpTests.cs` (greeting over `smtp` and
  `smtps`, 58 without a certificate, `STARTTLS` advertised or 454, anonymous delivery into the
  in-memory store, 530 without a login, the store's path, load, malformed refusal before the
  listener, lock released), three `ServerTlsCompositionTests`, and `HelpTextTests.Answer_Smtp_ListsEveryOptionTheSmtpServerReads`.
  Fast run: Surl.Cli.UnitTests 693, Surl.Console.UnitTests 268, all projects green.
- `dotnet format --verify-no-changes` reports only `ENDOFLINE` in the lane's working copy (LF
  checkouts under `* text=auto`, untouched files such as `SchemeDefaultPorts.cs` included); no
  other finding.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl serves smtp:// and smtps:// with SmtpProtocolServer into one mail store (in memory, or <path>/.surl/mail loaded at start, 37 when malformed); --help smtp and --aihelp smtp list it
