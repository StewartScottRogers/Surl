---
id: BL-309
title: Answer LDAP StartTLS, ldaps and SASL binds in Surl.Protocol.Ldap
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-308, BL-274]
touches: [Surl.Protocol.Ldap.UnitLibrary, Surl.Protocol.Ldap.UnitTests]
requirement: FR-052
created: 2026-09-30
completed:
---
# BL-309 — Answer LDAP StartTLS, ldaps and SASL binds in Surl.Protocol.Ldap

## Goal

`Surl.Protocol.Ldap` answers the `StartTLS` extended operation (upgrading the connection only when a
certificate is configured), serves a connection that is TLS from the first byte for `ldaps`, lists
`supportedSASLMechanisms` in the root DSE, and checks SASL binds - the ones the pinned `WinLDAP`
build sends for `--ntlm`, `--negotiate` and `--digest`, and the mechanisms `--auth` accepts - through
the SASL contract, as BL-284's ADR decides.

## Context

- Decisions: BL-284's ADR (the SASL mechanisms offered and their order, how each `WinLDAP` bind is
  checked, the multi-step bind with `saslBindInProgress` (14) and `serverSaslCreds`, the `StartTLS`
  answers with and without a certificate, which binds a TLS connection unlocks, the notes);
  ADR-0010 (upgrades, discarding bytes read past the upgrade request); ADR-0049 (the SASL contract:
  `IMailAuthenticationPolicy`, `MailLoginOffer`, `MailLoginStep`, `ISaslExchange`) - if BL-284's
  ADR renamed it protocol-neutral, that rename task is in this task's `depends-on`; ADR-0069 (BL-274: the TLS-upgrade constructor flag is named
  `isTlsUpgradeAvailable` - use that name here).
- Code: BL-308's server; the mail servers' `STARTTLS` handling (e.g.
  `Surl.Protocol.Smtp.UnitLibrary`) as the upgrade pattern, not a reference.
- Fixtures: `WinLDAP`'s NTLM, Negotiate and Digest binds recorded by BL-284 against the Windows
  reference build. `lib/openldap.c`'s `STARTTLS` and SASL binds have no pinned build yet (BL-282,
  BL-287): test them from RFC 4511 section 4.14, RFC 4513 and RFC 4422 byte layouts; BL-312 proves
  them against the pinned build once it exists.

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Ldap.UnitTests` show: `StartTLS` answered `success` and the connection
      upgraded with a certificate, refused with the ADR's code without one; bytes pipelined after the
      request discarded; the root DSE's `supportedSASLMechanisms` as the ADR orders them; each
      recorded `WinLDAP` SASL bind checked through a fake SASL policy and answered with the ADR's
      bytes; a multi-step bind; a refused mechanism; a bind over a TLS connection allowed where the
      plain-text rule refused it; no test opens a socket.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
