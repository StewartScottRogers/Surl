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
completed: 2026-09-30
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

- [x] A fast `CommandLineRunnerTests` test shows `smb://` and `smbs://` listen URLs start listeners
      with the SMB server (through `FakeListenerFactory`), `smbs://` needing `--cert` or
      `--self-signed`; `--version`'s `Protocols:` line lists `smb` and `smbs`.
- [x] `surl --help category` lists the SMB category; `--help <category>` lists every option the
      server reads; `--aihelp <topic>` answers with its `About` and example; `AiHelpTextTests`,
      `AiHelpFactsTests`, `CommandLineRunnerAiHelpTests`, `HelpTextTests` and `ManualTextTests` pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Delivered by ADR-0073 decisions 3, 6 and 9; no new ADR needed, every choice below follows it.
- `SmbProtocolServer.Schemes` already declares `smb` and `smbs` (as FTP, Gopher and MQTT declare
  their TLS scheme), so it is registered once, without the `ImplicitTlsSchemeServer` wrapper the
  task's Context suggested: wrapping it would claim `smbs` twice. `TlsSchemes.IsImplicitTls`
  already names `smbs`, so the engine secures it and asks for `--cert` or `--self-signed`.
- `--auth` gains `ntlmv1` (after `ntlm` in `OptionArgumentReader.AuthenticationMethodWords`,
  mapped to `AuthenticationMethod.NtlmV1` in `AuthenticationComposition`); its start-up warning
  is the existing `--auth: accepted methods are ...` line, which names it, as ADR-0073 decision 3
  says. The `--auth` explanation, `--aihelp auth` and the manual's ACCOUNTS section name it and
  why it is not in the default.
- The `smb` category holds decision 9's fourteen options; tests are in
  `CommandLineRunnerSmbTests` (a hand-built NT LM 0.12 negotiate answered through
  `FakeListenerFactory`) beside `CommandLineRunnerWsTests`, and the version pin in
  `CommandLineRunnerTests`.
- The `--aihelp smb` example binds port 0 and uses `--directory <path>` rather than decision 9's
  `445` and `./files`, so `CommandLineRunnerAiHelpTests` can run it like every other example; the
  curl line is decision 9's, with `<port>`. Its prose's exit codes (78, 9, 25, 67) come from
  ADR-0073's measurement table.
- `SchemeDefaultPorts` already had 445 for both schemes (ADR-0073 measured it), so it is untouched.
- Results: Surl.Cli.UnitTests 709 passed, Surl.Console.UnitTests 383 passed; whole fast run green;
  Measure-CodeQuality: Surl.Cli.UnitLibrary and Surl.Console 100% line, 100% branch, 0 failing.
- Lane 1 re-applied lane 2's two commits from factory/BL-299-lane-2-20260930-162236 (the code
  cherry-picked without conflict; only this task file conflicted) and re-verified on the current
  base: build clean, fast tests green (Surl.Cli.UnitTests 709, Surl.Console.UnitTests 383),
  Measure-CodeQuality 100% line and branch, 0 failing, for Surl.Cli.UnitLibrary and Surl.Console.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Backlog. Lane 2 could not integrate: push kept being refused. The work is on branch factory/BL-299-lane-2-20260930-162236; start with git cherry-pick --no-commit factory/BL-299-lane-2-20260930-162236 and fix it.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl serves smb:// and smbs:// with the SMB server, --auth accepts ntlmv1, and --help smb and --aihelp smb describe it
