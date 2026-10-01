---
id: BL-310
title: Register ldap and ldaps in surl with the directory, their help category and --aihelp topic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-307, BL-309]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-310 — Register ldap and ldaps in surl with the directory, their help category and --aihelp topic

## Goal

`surl ldap://127.0.0.1:<port>/` and `surl ldaps://...` serve one LDAP directory per `surl` run with
`LdapProtocolServer` - in memory, or loaded from `<path>/.surl/` with `--directory` - its binds
checked by `AuthenticationPolicy`, and `surl --help` and `--aihelp` list the `ldap` category and
topic.

## Context

- Decisions: BL-284's ADR (the directory's file, any seed option, the category and topic, how `ldaps`
  is claimed, the options the server reads); ADR-0031 decision 7 (probe, scheme check, lock, load
  service state, bind: load the directory after the lock, a malformed one ending surl with
  `CouldNotReadFile` (37) and the ADR's text before any listener binds); ADR-0010 (`ldaps` is TLS from
  the first byte; `StartTLS` offered when `--cert` or `--self-signed` is given, as for `smtp`);
  ADR-0034 decision 1 (`ldap` is one of curl's help categories) and ADR-0046 decision 3.
- Code: `Surl.Console/CommandLineRunner.cs` - `ComposeProtocolServers`, and `LoadServiceStateAsync`
  with the mail store's `ComposeMailStoreFiles`/`LoadMailStoreAsync` as the pattern for loading the
  directory; `ImplicitTlsSchemeServer` for `ldaps`; `ServerTlsComposition` for the upgrade's
  certificate; `AuthenticationComposition.cs` (the SASL policy). `Surl.Cli.UnitLibrary/HelpCategories.cs`,
  `CommandLineOptions.cs` (the options' categories, any new option with its
  `OptionArgumentReading`), `AiHelpProse.cs`, `AiHelpExamples.cs`, `ManualText.cs` (the data-directory
  section names the directory's file), `VersionText.cs`'s `Protocols:` line.
- Root `CLAUDE.md`'s `--aihelp` completeness rule: the topic list pinned in
  `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder` grows by one, and
  `CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic` passes.
- Keep this task to wiring; a server defect becomes a `Surl.Protocol.Ldap` task.

## Acceptance criteria

- [ ] A fast `CommandLineRunnerTests` test shows `ldap://` and `ldaps://` listen URLs start listeners
      with the LDAP server (through `FakeListenerFactory`), `ldaps://` needing `--cert` or
      `--self-signed`; `--version`'s `Protocols:` line lists `ldap` and `ldaps`.
- [ ] Fast tests show the directory loaded from `<path>/.surl/` with `--directory`, the ADR's in-memory
      start without it, and a malformed file refused with 37 and the ADR's text before any listener
      binds.
- [ ] `surl --help category` lists `ldap`; `--help ldap` lists every option the server reads;
      `--aihelp ldap` answers with its `About` and example; `AiHelpTextTests`, `AiHelpFactsTests`,
      `CommandLineRunnerAiHelpTests`, `HelpTextTests` and `ManualTextTests` pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
