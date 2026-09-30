---
id: BL-194
title: Check SASL PLAIN, LOGIN, XOAUTH2 and OAUTHBEARER logins in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-193]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-046
created: 2026-09-29
completed: 2026-09-29
---
# BL-194 — Check SASL PLAIN, LOGIN, XOAUTH2 and OAUTHBEARER logins in Surl.Authentication

## Goal

`Surl.Authentication`'s `AuthenticationPolicy` implements BL-193's SASL contract for the
clear-secret mechanisms - `PLAIN` (RFC 4616), `LOGIN`, `XOAUTH2` and `OAUTHBEARER` (RFC 7628) -
checking them against the accounts, refusing them over a connection without TLS unless
`--allow-plaintext-auth`, as BL-185's ADR decides.

## Context

- Decisions: BL-185's ADR (mechanism rules, `authzid` handling, what counts as plain-text,
  failure answers, `CheckedLogin` words); ADR-0032 sections 2, 3 and 8 (Bearer tokens are the
  empty-name accounts; `FixedTimeEquals` over SHA-256; the dummy account for an unknown user;
  the fixed 1-second delay for a refused credential on the injected `TimeProvider`; nothing
  distinguishes "no such user", "wrong password" and "no accounts").
- Code: `Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs`, `AccountBook.cs`,
  `CryptographicSecretComparer.cs`, `BearerAuthenticationMethod.cs` (the token rules),
  `Base64Credentials.cs`.
- `LOGIN` is the two-prompt exchange of draft-murchison-sasl-login; `XOAUTH2` is Google's format
  (`user=...^Aauth=Bearer ...^A^A`), its failure a JSON challenge as BL-185's ADR measured.
- ADR-0050 decision 2 (BL-184): an accepted `XOAUTH2` or `OAUTHBEARER` login's
  `MailLoginStep.AccountName` is the user the client sent (`user=`, or `OAUTHBEARER`'s `a=`),
  the mailbox owner the session then acts as; the token still matches only empty-name accounts.

## Acceptance criteria

- [x] Fast tests cover, for each mechanism: accepted with a matching account; refused for a wrong
      secret, an unknown user and no accounts, each the same verdict after the 1-second delay;
      refused as plain-text over no TLS and accepted with `--allow-plaintext-auth`; accepted
      unchecked with `--allow-anonymous`; a malformed response (bad base64, missing field) refused;
      the `CheckedLogin` note naming the mechanism and user and never the secret.
- [x] A fast test shows an accepted `XOAUTH2` and `OAUTHBEARER` login carrying the sent user
      name as `MailLoginStep.AccountName` (ADR-0050 decision 2).
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- The mechanisms follow ADR-0049 (BL-185's ADR) as written, so no new ADR: offer order
  OAUTHBEARER, XOAUTH2, PLAIN, LOGIN; all four are plain-text, so offered and run only over TLS
  or with `--allow-plaintext-auth`; `--allow-anonymous` runs every step and accepts unchecked.
- Bearer mechanisms check the token against the empty-name account and do not match `user=` or
  `a=`, since a token account has no name (ADR-0032, section 1). An accepted one's
  `MailLoginStep.AccountName` is the `user=` or `a=` sent (the empty name when `a=` is left out,
  `=2C` and `=3D` decoded), the mailbox owner the session acts as (ADR-0050, decision 2; added
  while rebasing onto the criterion BL-184 filed). A refused token gets the
  RFC 7628 error challenge `{"status":"invalid_token"}` after the delay; the client's reply ends it.
- PLAIN grants no proxy rights: an `authzid` other than empty or the `authcid` is refused, after
  the password comparison, so it costs what a wrong password does.
- `AuthenticationPolicy.CheckApopLoginAsync` answers `RefusedMechanism` until BL-195 builds APOP.
- Resumed run: the test helpers wrote `"\x01auth"`, which C# reads as `\x01a` (U+001A); they now
  use `\u0001`. The secret-in-note test used `tok`, which `bearer token` contains; it now uses
  a distinctive secret. `PlainSaslExchange.TrySplit` was split to get under complexity 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Authentication checks SASL PLAIN, LOGIN, XOAUTH2 and OAUTHBEARER logins as ADR-0049 decides
