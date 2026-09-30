---
id: BL-209
title: Register pop3 and pop3s in surl with their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-207, BL-206]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-045
created: 2026-09-29
completed:
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

- [ ] A fast `CommandLineRunnerTests` test shows `pop3://` and `pop3s://` listen URLs start
      listeners with the POP3 server, sharing the SMTP server's store instance; `--version`'s
      `Protocols:` line lists `pop3` and `pop3s`.
- [ ] `surl --help category` lists `pop3`; `--help pop3` lists every option the server reads;
      `--aihelp pop3` answers with its `About` and example; `CommandLineRunnerAiHelpTests`,
      `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-29: Created.
