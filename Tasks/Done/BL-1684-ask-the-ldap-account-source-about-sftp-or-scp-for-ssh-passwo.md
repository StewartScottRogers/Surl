---
id: BL-1684
title: Ask the LDAP account source about sftp or scp for SSH password logins
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0149-how-an-ldap-directory-serves-as-an-optional-account-source.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1684 — Ask the LDAP account source about sftp or scp for SSH password logins

## Goal

An SSH password login on a server given `--user-ldap ...;protocols=sftp` (or `scp`) is checked against the directory
on that scheme.

## Context

- `AuthenticationPolicy.CheckSshPasswordLoginAsync` asks the directory about the family name `ssh`
  (`SshLoginScheme`), so `protocols=sftp` or `scp` refuses directory passwords. It fails closed.
- BL-1669 made the HTTP family ask about its view's scheme (`AuthenticationSettings.HttpDirectoryScheme`, ADR-0149
  amendment 2); the same fits SSH, whose `sftp` and `scp` servers are composed with separate views.
- Login events and fault points keep naming the family `ssh` unless the login carries a `LoginExchange`.
- No new wire bytes: the SSH answers to an accepted or refused password are today's.

## Acceptance criteria

- [x] A named test shows `protocols=sftp` on the `sftp` view contacts the directory for a password login and on the
      `scp` view does not.
- [x] A named test shows `protocols=ssh` behaviour is either kept or refused at start, and ADR-0149 records which.
- [x] `dotnet build` is clean, the fast tests are green, and every changed member keeps 100% line and branch
      coverage.

## Notes

- Plan: SSH password logins ask the directory about `AuthenticationSettings.DirectorySchemeFor(SshLoginScheme)`, the
  view's scheme, or `ssh` on settings that are no view. It replaces `HttpDirectoryScheme`, so HTTP and SSH share one
  rule. Composition already builds `scp` and `sftp` over separate views, so it is unchanged.
- `protocols=ssh` is refused at start (decided in ADR-0149 amendment 3): `Surl.Cli` already refused it as an unknown
  scheme, so only a test pins it (`Parse_UserLdapNamingSsh_IsRefusedAtStart`).
- Tests: `SshPasswordLogin_OnASchemesView_AsksTheDirectoryOnlyWhenItServesThatScheme` and
  `SshPasswordLogin_OnSettingsThatAreNoView_AsksTheDirectoryAboutSsh`; 3 view cases failed before the change.
- `touches` gained `Application/Surl.Cli.UnitTests` (that one test) and the ADR-0149 file; no task in `Doing` named
  either.
- The `/feature` stages ran in this session, without subagents: the change is a few lines. No upstream curl
  measurement: no wire byte changes.
- Verified 2026-11-25: build 0 warnings; fast tests 28919 passed, 0 failed, 3 skipped; format check clean on the
  changed files; `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. SSH password logins ask the LDAP directory about the server's own scheme, so protocols=sftp or scp admits directory logins there only; protocols=ssh stays refused at start
