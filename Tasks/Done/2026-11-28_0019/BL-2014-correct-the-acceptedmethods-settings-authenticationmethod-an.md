---
id: BL-2014
title: Correct the AcceptedMethods, Settings, AuthenticationMethod and library CLAUDE.md descriptions in Surl.Authentication
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2014 — Correct the AcceptedMethods, Settings, AuthenticationMethod and library CLAUDE.md descriptions in Surl.Authentication

## Goal

The `AcceptedMethods`, `Settings` and `AuthenticationMethod` XML descriptions and the SSH and SASL
statements of the library's `CLAUDE.md` are true of the code.

## Context

Found while working BL-1994 (2026-11-27), read from source. Documentation only; every file is in
`Foundation/Surl.Authentication.UnitLibrary`.

- `AuthenticationSettings.cs`, the `AcceptedMethods` parameter: not true of the proxy policy
  (`--proxy-auth`) or of the `kafka` and `xmpp` views, to which `Surl.Composition` adds SCRAM.
- `AuthenticationPolicy.cs`, the `Settings` property summary: leaves out `LdapAccountSource` and
  `RequiresLoginWithoutAccounts`.
- `AuthenticationPolicy.cs`, the class summary's first sentence: `Surl.Composition` also composes
  `NoAccountProxyAuthenticationPolicy` and `AnonymousAuthenticationPolicy` as an `IAuthenticationPolicy`.
- `AuthenticationMethod.cs`: members say "for the mail servers" where LDAP, AMQP and other servers
  run the same SASL exchanges.
- `CLAUDE.md`, "SSH logins (BL-157)": says a password is checked through
  `AccountBook.CheckPassword`, and leaves out the LDAP account source.
- `CLAUDE.md`, the opening "Today it holds" list: the SASL mechanisms leave out the three SCRAM ones.

## Acceptance criteria

- [x] The `AcceptedMethods` description is true of every composer of the settings in `Surl.Composition`.
- [x] The `Settings` summary and the class summary's first sentence are true of the code.
- [x] No `AuthenticationMethod` member's summary limits it to the mail servers when another server runs it.
- [x] The library's `CLAUDE.md` names the LDAP account source for SSH password logins and the three SCRAM mechanisms in its opening list.
- [x] The diff changes only `///` lines and the library's `CLAUDE.md`.
- [x] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean.

## Notes

- Delivered by `align-and-document`; statements checked against `Surl.Composition` and the servers.
- Also corrected in the same files: the `AuthenticationMethod` type summary (the declared order covers
  HTTP challenges and option warnings, not the SASL offer), the opening `CLAUDE.md` list's
  `GSS-SPNEGO`, and where SCRAM's server-final is success data and where a challenge.
- The class summary no longer claims to be the only `IAuthenticationPolicy` composed.
  `AnonymousAuthenticationPolicy` is not named in it: the search found it as a property default and
  inside `NoAccountProxyAuthenticationPolicy`.
- `dotnet build -warnaserror` and `dotnet format --verify-no-changes` clean; fast tests 30350 passed,
  3 skipped, 0 failed.
- Follow-ups filed: BL-2056 (further descriptions in this library), BL-2057 (`ComposeWithoutAccounts`).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Surl.Authentication's AcceptedMethods, Settings, AuthenticationMethod and CLAUDE.md descriptions are true of the code.
