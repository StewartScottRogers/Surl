---
id: BL-2022
title: Make the PasswordLoginVerdict member comments true of every case each verdict covers
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary/PasswordLoginVerdict.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-2022 — Make the PasswordLoginVerdict member comments true of every case each verdict covers

## Goal

The XML comments of the `PasswordLoginVerdict` members `Accepted`, `RefusedCredentials` and `RefusedAnonymous` are
true of every case in which `AuthenticationPolicy` returns them.

## Context

Found while working BL-2005 (2026-11-27), read from source. `///` comment text only; no code or behaviour change.

The file is `Foundation/Surl.Protocol.Abstractions.UnitLibrary/PasswordLoginVerdict.cs`. The code it describes is
`CheckPasswordLoginAsync` and `JudgePasswordLogin` in
`Foundation/Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs` (read it, do not edit it).

- `RefusedAnonymous` says "The login carried no credentials and anonymous logins are not allowed."
  `JudgePasswordLogin` returns it for any login whose `UserName` is `null`, with or without a password, once
  `--allow-anonymous` is off and the plain-text refusal (`RefusedPlaintext`, checked first) has not applied. The OCI
  registry's probe (`OciAccessCheck`, no user name, an empty password over plain text) gets it under
  `--allow-plaintext-auth`.
- `Accepted` says "The login's credentials were checked and match an account." `CheckPasswordLoginAsync` also returns
  it when the account book holds no account of the name and the LDAP account source accepts the name and password
  (ADR-0149).
- `RefusedCredentials` says "The user name or password does not match an account." It is also returned when a token
  (`PasswordLogin.IsToken`) matches no empty-name account, when the directory does not accept the login, and when
  credentials that matched are refused by a `refuse-login` fault rule (`ConcludeCheckedLoginAsync`, ADR-0124
  decision 4).

BL-2005 corrects other files of the same library and BL-2008 concerns `CommandStageFaults`; neither covers this file.
No store, event or fault changes: documentation only.

## Acceptance criteria

- [ ] The `RefusedAnonymous` comment says the login has no user name, and does not say it carried no credentials or no password.
- [ ] The `Accepted` comment covers both a match with a local account and acceptance by an LDAP account source.
- [ ] The `RefusedCredentials` comment covers a token that matches nothing and refusal by a `refuse-login` fault, beside a user name or password that no account or directory accepts.
- [ ] The diff changes only `///` lines in `PasswordLoginVerdict.cs`.
- [ ] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
