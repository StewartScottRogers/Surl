---
id: BL-2005
title: Correct the PasswordLogin, AnonymousAuthenticationPolicy and NamesAccount statements in Abstractions
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary/PasswordLogin.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/AnonymousAuthenticationPolicy.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2005 — Correct the PasswordLogin, AnonymousAuthenticationPolicy and NamesAccount statements in Abstractions

## Goal

The `PasswordLogin` and `AnonymousAuthenticationPolicy` comments and the library notes on `NamesAccount` are true of
the code.

## Context

Found while working BL-1981 (2026-11-27), read from source. Comment and notes text only; no code or behaviour change.
All files are in `Foundation/Surl.Protocol.Abstractions.UnitLibrary`.

- `PasswordLogin.cs`: the summary says "A login by user name and clear password". The record also carries a token
  (`IsToken`), a login with no user name (`UserName` null) and one with no password (`Password` null), and servers send
  such records to ask what the policy allows (`OciAccessCheck`). The `Scheme` parameter says "for the log";
  `AuthenticationPolicy.CheckPasswordLoginAsync` (`Surl.Authentication`) also asks the LDAP directory about it, and
  names the `LoginJudgedEvent` and the fault point by it unless `Exchange` carries a scheme.
- `AnonymousAuthenticationPolicy.cs`: the summary ends "the policy a server's policy-less constructor passes until
  BL-117 composes the real one". BL-117 is done. Today it is the default of `HttpProxyProtocolServer`'s one-argument
  constructor, of `XmppServerOptions.AuthenticationPolicy` and of the proxy policy properties of `Surl.Composition`'s
  `ComposedAuthentication` and `ComposedProtocolServices`, and `NoAccountProxyAuthenticationPolicy` delegates to it.
  Search again before writing.
- `CLAUDE.md`, "The authentication contract": gives the Azure Storage path-style check as the only use of
  `NamesAccount`. `OciAccessCheck` and `McpAuthorizationGrants` also call it, to refuse a token whose account the
  account book no longer holds (ADR-0167 decision 4). Describe what the code does today; BL-2003 decides about
  directory users separately.

`IAuthenticationPolicy.cs` is BL-1981's and is not touched here. No store, event or fault changes: documentation only.

## Acceptance criteria

- [x] The `PasswordLogin` summary covers the user name and password, token and missing-credential forms.
- [x] The `Scheme` parameter comment no longer gives the log as the scheme's only use.
- [x] The `AnonymousAuthenticationPolicy` summary no longer mentions BL-117 as work to come, and every use it names exists in the code.
- [x] The library's `CLAUDE.md` names the token check beside the Azure Storage path-style check for `NamesAccount`.
- [x] The diff changes only `///` lines in the two `.cs` files, and `CLAUDE.md`.
- [x] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

- Delivered by `align-and-document`; no behaviour change and no new design decision, so no ADR.
- Also corrected in the same comments, because they were false: the `UserName` and `Password` parameters said "as
  sent" (LDAP, Redis, token logins, FTP and STOMP differ), and the `AnonymousAuthenticationPolicy` summary called its
  answers "the behaviour of `--allow-anonymous`", although the real policy still checks some logins under it.
- Uses of `AnonymousAuthenticationPolicy` found beyond the Context's list: the SMTP, IMAP and POP3 upstream test case
  servers' constructors take it.
- Found and left alone: the `HttpLoginScheme` and `SmbLoginScheme` summaries (BL-1994), the `OciAccessCheck` `policy`
  parameter (BL-2007). The other false statements found are filed as follow-up tasks.
- Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings, 0 errors; `dotnet format --verify-no-changes` clean;
  fast tests 30401 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. PasswordLogin, AnonymousAuthenticationPolicy and NamesAccount statements now match the code; follow-ups BL-2013 to BL-2016 filed
