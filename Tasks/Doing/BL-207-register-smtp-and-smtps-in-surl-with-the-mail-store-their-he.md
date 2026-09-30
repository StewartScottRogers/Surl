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
completed:
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

- [ ] A fast `CommandLineRunnerTests` test shows `smtp://` and `smtps://` listen URLs start
      listeners with the SMTP server (through `FakeListenerFactory`), `smtps://` needing `--cert`
      or `--self-signed`; `--version`'s `Protocols:` line lists `smtp` and `smtps`.
- [ ] Fast tests show the store loaded from `<path>/.surl/mail` with `--directory`, in memory
      without it, and a malformed store refused with 37 and the ADR's text before any listener
      binds.
- [ ] `surl --help category` lists `smtp`; `--help smtp` lists every option the server reads;
      `--aihelp smtp` answers with its `About` and example; `CommandLineRunnerAiHelpTests`,
      `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
