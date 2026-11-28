---
id: BL-1994
title: Correct the login scheme, SASL list, accounts and default-method summaries in Surl.Authentication
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1994 — Correct the login scheme, SASL list, accounts and default-method summaries in Surl.Authentication

## Goal

The XML summaries of the login scheme constants, `AuthenticationPolicy`, the `AuthenticationSettings.Accounts`
parameter and `AuthenticationMethods.DefaultAccepted` are true of the code.

## Context

Found while working BL-1963 (2026-11-27), read from source. `///` comment text only; no code or behaviour change. All
files are in `Foundation/Surl.Authentication.UnitLibrary`.

- `AuthenticationPolicy.cs`, the `HttpLoginScheme` and `SmbLoginScheme` summaries: both say the request does not carry
  its listen URL's scheme. `PublishJudgedLogin` uses the `LoginExchange`'s scheme when the request has one, so the
  family name applies only without one. `SshLoginScheme`'s summary already says so.
- `AuthenticationPolicy.cs`, class summary: the SASL mechanisms listed as "today" leave out `SCRAM-SHA-256`,
  `SCRAM-SHA-512`, `SCRAM-SHA-1` (all in `SaslMechanism.InOfferOrder`) and `GSS-SPNEGO`; "with no accounts every
  login is refused" does not mention an `LdapAccountSource`, which can accept a name the account book does not hold.
- `AuthenticationSettings.cs`, the `Accounts` parameter: "Every account from `--user` and `--user-file`" leaves out
  the `--mailbox` and `--mailbox-file` accounts, accounts added while the instance runs, and that the proxy policy
  holds the `-U` and `--proxy-user-file` accounts instead.
- `AuthenticationMethods.cs`, the `DefaultAccepted` summary: its "left out" list omits `GSSAPI` and the three SCRAM
  methods.

No store, event or fault changes: documentation only.

## Acceptance criteria

- [ ] The `HttpLoginScheme` and `SmbLoginScheme` summaries say the family name is used when the request carries no `LoginExchange`.
- [ ] The `AuthenticationPolicy` class summary names every mechanism of `SaslMechanism.InOfferOrder` and `GSS-SPNEGO`, and says when a login can be accepted with no local account.
- [ ] The `AuthenticationSettings.Accounts` description is true of every composer of the settings in `Surl.Composition`.
- [ ] The `DefaultAccepted` summary's left-out methods are exactly the `AuthenticationMethod` members the set does not hold.
- [ ] The diff changes only `///` lines in the library.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
