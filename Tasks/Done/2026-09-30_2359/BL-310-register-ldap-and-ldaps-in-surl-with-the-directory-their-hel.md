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
completed: 2026-09-30
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

- [x] A fast `CommandLineRunnerTests` test shows `ldap://` and `ldaps://` listen URLs start listeners
      with the LDAP server (through `FakeListenerFactory`), `ldaps://` needing `--cert` or
      `--self-signed`; `--version`'s `Protocols:` line lists `ldap` and `ldaps`.
- [x] Fast tests show the directory loaded from `<path>/.surl/` with `--directory`, the ADR's in-memory
      start without it, and a malformed file refused with 37 and the ADR's text before any listener
      binds.
- [x] `surl --help category` lists `ldap`; `--help ldap` lists every option the server reads;
      `--aihelp ldap` answers with its `About` and example; `AiHelpTextTests`, `AiHelpFactsTests`,
      `CommandLineRunnerAiHelpTests`, `HelpTextTests` and `ManualTextTests` pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member in `Surl.Cli.UnitLibrary` and
      `Surl.Console`.

## Notes

- Wiring, as the mail store's: `CommandLineRunner.ComposeLdapDirectoryFile` names
  `<path>/.surl/ldap/directory.ldif` with `--directory` (none without), and `LoadLdapServerAsync`
  builds the `LdapProtocolServer` - `LoadAsync` over the file, the public constructor's empty
  directory without one - after the retained messages and the mail store, under the lock and before
  any listener binds; an `LdapDirectoryLoadException` becomes `surl: (37) Could not read <file>:
  <reason>`. The server is part of `ServiceState` (it is what holds the directory), registered for
  `ldap` and, through `ImplicitTlsSchemeServer`, `ldaps`. The policy is the one
  `AuthenticationPolicy`, as both `IAuthenticationPolicy` and `ISaslAuthenticationPolicy`.
  `StartTLS`: `ldap` joins `ServerTlsComposition`'s upgradable schemes, so `--cert` or
  `--self-signed` makes the TLS settings and the root DSE lists `1.3.6.1.4.1.1466.20037`.
- The listener tests are in `CommandLineRunnerLdapTests` (the RTSP and mail servers' pattern: one
  `CommandLineRunner<Protocol>Tests` class per server); `--version`'s `Protocols:` line is pinned in
  `CommandLineRunnerTests`. They replay the pinned Windows build's `simple-bind-base-search` bytes
  (`Surl.Protocol.Ldap.UnitTests/Fixtures`) and read the responses with `System.Formats.Asn1`.
- Help: the `ldap` category (`LDAP protocol`, schemes `ldap`, `ldaps`) holds ADR-0072 decision 8's
  thirteen options; the `ldap` topic has five `About` paragraphs and a `--allow-plaintext-auth -u
  alice:secret` example reached with `curl -u alice:secret ldap://127.0.0.1:<port>/` (the root DSE,
  which an empty directory answers). The content, auth and tls topics and the manual's DATA
  DIRECTORY section name the directory, the LDAP simple bind and `ldaps`; the manual gains LDAP
  OPTIONS.
- With `ldap` and `ldaps` registered, every scheme `SchemeDefaultPorts` accepts has a server, so
  `CommandLineRunner`'s own unregistered-scheme check could no longer be reached (a branch the 100%
  coverage gate would fail) and is removed: `ListenUrlParser` refuses every other scheme with the
  same `surl: (1) Protocol "<scheme>" not supported`. `CommandLineRunnerTests.
  ComposeRegisteredSchemes_AreEverySchemeAListenUrlMayName` keeps the two lists equal. No input
  changes behaviour: an unknown scheme was already the parser's refusal. The unserved-scheme
  example and tests move to `rtmp` (curl has it, surl does not), and the two log tests that showed
  `-s` hiding a failure and `-s -S` showing it now use `https` without a certificate (58), a
  failure the runner writes, since a command-line refusal is written at every level.
- Out of `touches`, left for `align-and-document`: `Documentation/Product/Product-Overview.md` and
  the glossary may list the served schemes.
- `Measure-CodeQuality.ps1`: `Surl.Cli.UnitLibrary` and `Surl.Console` at 100% line and branch, no
  failing member. It also reports two pre-existing complexity failures in
  `Surl.Conformance.UnitLibrary` (BL-332's), filed as BL-343.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl serves ldap and ldaps over the directory in .surl/ldap/directory.ldif (empty in memory), with the ldap help category and --aihelp topic
