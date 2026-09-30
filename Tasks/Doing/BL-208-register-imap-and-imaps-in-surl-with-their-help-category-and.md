---
id: BL-208
title: Register imap and imaps in surl with their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-207, BL-203, BL-204]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-044
created: 2026-09-29
completed:
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

- [ ] A fast `CommandLineRunnerTests` test shows `imap://` and `imaps://` listen URLs start
      listeners with the IMAP server, sharing the SMTP server's store instance; `--version`'s
      `Protocols:` line lists `imap` and `imaps`.
- [ ] `surl --help category` lists `imap`; `--help imap` lists every option the server reads;
      `--aihelp imap` answers with its `About` and example; `CommandLineRunnerAiHelpTests`,
      `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
