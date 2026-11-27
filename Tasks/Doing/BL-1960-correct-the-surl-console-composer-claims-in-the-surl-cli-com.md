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
completed:
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

- [ ] `CommandLineAccount.cs` names `Surl.Composition` as the owner of `AuthenticationComposition`.
- [ ] Each other `Surl.Console` sentence listed above names the project that does what it says, checked against source.
- [ ] The `AuthenticationComposition` class summary is true of which servers receive the policy.
- [ ] Only XML comments change, `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
