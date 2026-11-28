---
id: BL-1960
title: Correct the Surl.Console composer claims in the Surl.Cli comments and the AuthenticationComposition summary
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Composition.UnitLibrary/AuthenticationComposition.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1960 — Correct the Surl.Console composer claims in the Surl.Cli comments and the AuthenticationComposition summary

## Goal

The `Surl.Cli` XML comments name the project that really does each job, and the `AuthenticationComposition` class
summary is true of which servers receive the authentication policy.

## Context

Found while working BL-1949 (2026-11-27). Comment text only; no behaviour change. Line numbers are as of that date.

- `CommandLineAccount.cs:6` says "`Surl.Console`'s `AuthenticationComposition`"; the type is in `Surl.Composition`.
- These name `Surl.Console` and have not been checked against the code: `CommandLineAuthorizedKeys.cs:6`,
  `CommandLineParser.cs:14`, `ListenUrlParser.cs:16`, `OptionArgumentReader.cs:473`, `VersionText.cs:9`. Check each
  against source; a sentence that is true stays.
- `Application/Surl.Composition.UnitLibrary/AuthenticationComposition.cs:10` says the policy is given to "the HTTP,
  MQTT, SMTP and SSH servers"; more servers receive it (`ProtocolServerRegistrations.Original`).

## Acceptance criteria

- [x] `CommandLineAccount.cs` names `Surl.Composition` as the owner of `AuthenticationComposition`.
- [x] Each other `Surl.Console` sentence listed above names the project that does what it says, checked against source.
- [x] The `AuthenticationComposition` class summary is true of which servers receive the policy.
- [x] Only XML comments change, `dotnet build` is clean and the fast tests pass.

## Notes

- Done in the lane session without an `align-and-document` subagent: seven comment edits.
- Sentence by sentence, against source:
  - `CommandLineAuthorizedKeys`: `Surl.Composition`'s `AuthenticationComposition` reads the file.
  - `CommandLineParser`: the scheme is refused by `ListenUrlParser` during parsing, not later; the data directory
    is checked by `Surl.Composition`'s `SurlServerComposition`.
  - `ListenUrlParser`: rule 3 is applied by the parser itself.
  - `OptionArgumentReader`: `Surl.Composition`'s `SshAlgorithmComposition` checks the names.
  - `VersionText`: still names `Surl.Console`, which reads the version; the schemes come from `Surl.Composition`.
- The `AuthenticationComposition` summary also now names the mailbox accounts, `--user-ldap`, `--keytab` and the
  separate proxy policy, and no longer says every loosening option's warning is written there.
- `Surl.Cli.UnitLibrary/CLAUDE.md`'s `Surl.Console` sentences were checked in passing and left as they are.
- Verify: 0 C# files rewritten, build 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests all passed.
  Integration tests not run: no behaviour changed.
- Filed BL-1988: ADR-0007 section 4 rule 3 still says `Surl.Console` refuses an unregistered scheme.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Surl.Cli comments and the AuthenticationComposition summary name the projects and servers that do each job
