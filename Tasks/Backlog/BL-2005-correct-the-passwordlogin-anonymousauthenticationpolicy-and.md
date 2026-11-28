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
completed:
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

- [ ] The `PasswordLogin` summary covers the user name and password, token and missing-credential forms.
- [ ] The `Scheme` parameter comment no longer gives the log as the scheme's only use.
- [ ] The `AnonymousAuthenticationPolicy` summary no longer mentions BL-117 as work to come, and every use it names exists in the code.
- [ ] The library's `CLAUDE.md` names the token check beside the Azure Storage path-style check for `NamesAccount`.
- [ ] The diff changes only `///` lines in the two `.cs` files, and `CLAUDE.md`.
- [ ] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
