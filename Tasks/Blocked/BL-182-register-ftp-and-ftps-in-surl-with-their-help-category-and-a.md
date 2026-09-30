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
completed:
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

- [ ] A fast `CommandLineRunnerTests` test shows `ftp://` and `ftps://` listen URLs start
      listeners with the FTP server (through `FakeListenerFactory`), `ftps://` needing `--cert` or
      `--self-signed` as ADR-0032 section 10 says; `--version`'s `Protocols:` line lists `ftp`
      and `ftps`.
- [ ] `surl --help category` lists `ftp`; `--help ftp` lists every option the server reads;
      `--aihelp ftp` answers with its `About` and example; `CommandLineRunnerAiHelpTests`,
      `AiHelpFactsTests`, `HelpTextTests` and `ManualTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Blocked. Stewart: Surl dark factory timed out after 120 min; see Z:\repos\Surl.logs\BL-182-20260930-070942-L6.jsonl
