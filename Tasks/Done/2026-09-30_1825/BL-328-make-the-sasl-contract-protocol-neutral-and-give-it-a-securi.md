---
id: BL-328
title: Make the SASL contract protocol-neutral and give it a security layer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests, Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests, Surl.Protocol.Pop3.UnitLibrary, Surl.Protocol.Pop3.UnitTests, Surl.Console, Surl.Console.UnitTests, Documentation/Wiki/Glossary.md]
requirement: FR-049
created: 2026-09-30
completed: 2026-09-30
---
# BL-328 — Make the SASL contract protocol-neutral and give it a security layer

## Goal

The SASL contract in `Surl.Protocol.Abstractions` serves LDAP as well as mail, as ADR-0072
decision 4 ("The contract") decides: `SaslLoginStep`, `SaslLoginOutcome`, a new
`ISaslAuthenticationPolicy` that `IMailAuthenticationPolicy` extends, `SaslExchangeStart.CanCarrySecurityLayer`
and `ISaslSecurityLayer` on an accepted step, with no change to what any mail server sends.

## Context

- Decision: ADR-0072 decision 4, amending ADR-0049 decision 6. Measured there: after an NTLM or
  `DIGEST-MD5` bind, upstream curl's `WinLDAP` protects every later message, so the contract must
  hand the server a security layer.
- Renames: `MailLoginStep` to `SaslLoginStep`, `MailLoginOutcome` to `SaslLoginOutcome`, everywhere
  (Abstractions, `Surl.Authentication`, the SMTP, IMAP and POP3 servers and their tests,
  `Surl.Console`'s composition, the glossary). `IMailAuthenticationPolicy`, `MailLoginOffer` and
  `CheckApopLoginAsync` keep their names: they are mail-only.
- New: `ISaslAuthenticationPolicy { IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request);
  ISaslExchange StartSaslExchange(SaslExchangeStart start); }` (`SaslOfferRequest` carries the
  scheme and `TlsSession?`); `IMailAuthenticationPolicy : ISaslAuthenticationPolicy`;
  `SaslExchangeStart.CanCarrySecurityLayer` (default `false`); `SaslLoginStep.SecurityLayer`
  (`ISaslSecurityLayer?`, default `null`); `ISaslSecurityLayer { int MaximumProtectedBytes; byte[]
  Protect(ReadOnlySpan<byte> message); bool TryUnprotect(ReadOnlySpan<byte> buffer, out byte[]
  message); }`. `AuthenticationPolicy` and `AnonymousAuthenticationPolicy` implement
  `GetSaslMechanisms` with ADR-0049 decision 2's order plus `GSS-SPNEGO` (`negotiate`) after
  `GSSAPI` when the scheme is `ldap` or `ldaps`.
- One public type per file; `Surl.Protocol.Abstractions.UnitTests`' reference rules still hold.

## Acceptance criteria

- [x] No type named `MailLoginStep` or `MailLoginOutcome` remains (`git grep -n "MailLogin\(Step\|Outcome\)"`
      finds nothing outside `Tasks/` and `Documentation/Planning/Decisions/`), and the glossary names
      the new types.
- [x] Tests in `Surl.Authentication.UnitTests` show `GetSaslMechanisms` for `smtp` equal to
      `GetMailLoginOffer(...).SaslMechanisms` for the same connection, and for `ldap` with `GSS-SPNEGO`
      placed after `GSSAPI` when `negotiate` is accepted.
- [x] Every existing SMTP, IMAP and POP3 test passes unchanged apart from the renames.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for every touched library.

## Notes

- Filed by BL-284 (ADR-0072). BL-309 depends on it.
- Delivered directly rather than through a protocol-architect plan: ADR-0072 decision 4 already
  fixes every type and member, so the plan was the ADR.
- New in Abstractions: `ISaslAuthenticationPolicy`, `SaslOfferRequest`, `ISaslSecurityLayer`;
  `SaslExchangeStart.CanCarrySecurityLayer` and `SaslLoginStep.SecurityLayer` are trailing
  optional parameters, so every existing construction compiles unchanged.
- Choice: `AuthenticationPolicy.GetSaslMechanisms` is the mail offer's mechanism list for every
  scheme (one private `OfferedSaslMechanismNames` feeds both), with `GSS-SPNEGO` inserted right
  after `GSSAPI` (or first, when `GSSAPI` is not offered) for `ldap`/`ldaps`, matched
  case-insensitively, when `--auth` accepts `negotiate`. `GSS-SPNEGO` is offered on any
  connection since it sends no plain-text secret. `StartSaslExchange("GSS-SPNEGO")` stays
  `RefusedMechanism` until BL-329 builds the exchange.
- Choice: `AnonymousAuthenticationPolicy.GetSaslMechanisms` offers `PLAIN` alone for every scheme,
  its mail offer: ADR-0072 refuses unchecked NTLM and `DIGEST-MD5` because their security layer
  needs the password, so the anonymous double offers no mechanism that would need one.
- The test doubles in the SMTP, IMAP and POP3 tests gained `GetSaslMechanisms` (their mail offer's
  list); no existing test changed beyond the renames.
- Coverage: `Measure-CodeQuality.ps1` reports 100% line and branch, 0 failing members, for
  Abstractions, Authentication, SMTP, IMAP, POP3 and Console.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The SASL contract is protocol-neutral: SaslLoginStep/SaslLoginOutcome, ISaslAuthenticationPolicy with GetSaslMechanisms (GSS-SPNEGO for LDAP), and ISaslSecurityLayer on an accepted step
