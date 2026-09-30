---
id: BL-299
title: Register smb and smbs in surl with their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-298, BL-295]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-050
created: 2026-09-30
completed:
---
# BL-299 — Register smb and smbs in surl with their help category and --aihelp topic

## Goal

`surl smb://127.0.0.1:<port>/` and `surl smbs://...` serve files with `SmbProtocolServer`, its logins
checked by `AuthenticationPolicy` through BL-294's contract, and `surl --help` and `--aihelp` list the
SMB category and topic.

## Context

- Decisions: BL-283's ADR (the category name, how `smbs` is claimed, any new option or `--auth` word
  for NTLMv1 and the start-up warning it writes); ADR-0010 (`smbs` is TLS from the first byte, with a
  certificate from `--cert` or `--self-signed`); ADR-0034 decision 1 and ADR-0046 decision 3
  (category row, option categories, topic `About` and example in the same change).
- Code: `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` (register the server, with
  `ImplicitTlsSchemeServer` for `smbs` as for `smtps`), `Surl.Console/AuthenticationComposition.cs`;
  `Surl.Cli.UnitLibrary/HelpCategories.cs`, `CommandLineOptions.cs` (the options the server reads in
  the new category; any new option parsed with its `OptionArgumentReading`), `AiHelpProse.cs`,
  `AiHelpExamples.cs`, `ManualText.cs`, `SchemeDefaultPorts.cs` (only if BL-283's measurement changed
  ADR-0007's unmeasured 445), and `VersionText.cs`'s `Protocols:` line.
- Root `CLAUDE.md`'s `--aihelp` completeness rule: the topic list pinned in
  `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder` grows by one, and
  `CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic` passes.
- Keep this task to wiring; a server defect becomes a `Surl.Protocol.Smb` task.

## Acceptance criteria

- [ ] A fast `CommandLineRunnerTests` test shows `smb://` and `smbs://` listen URLs start listeners
      with the SMB server (through `FakeListenerFactory`), `smbs://` needing `--cert` or
      `--self-signed`; `--version`'s `Protocols:` line lists `smb` and `smbs`.
- [ ] `surl --help category` lists the SMB category; `--help <category>` lists every option the
      server reads; `--aihelp <topic>` answers with its `About` and example; `AiHelpTextTests`,
      `AiHelpFactsTests`, `CommandLineRunnerAiHelpTests`, `HelpTextTests` and `ManualTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-30: Created.
