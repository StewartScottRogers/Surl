---
id: BL-309
title: Answer LDAP StartTLS, ldaps and SASL binds in Surl.Protocol.Ldap
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-308, BL-274, BL-328, BL-329, BL-326]
touches: [Surl.Protocol.Ldap.UnitLibrary, Surl.Protocol.Ldap.UnitTests]
requirement: FR-052
created: 2026-09-30
completed: 2026-09-30
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

- [x] Tests in `Surl.Protocol.Ldap.UnitTests` show: `StartTLS` answered `success` and the connection
      upgraded with a certificate, refused with the ADR's code without one; bytes pipelined after the
      request discarded; the root DSE's `supportedSASLMechanisms` as the ADR orders them; each
      recorded `WinLDAP` SASL bind checked through a fake SASL policy and answered with the ADR's
      bytes; a multi-step bind; a refused mechanism; a bind over a TLS connection allowed where the
      plain-text rule refused it; no test opens a socket.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

- BL-284 (ADR-0072 decision 4): `WinLDAP` seals every message after an NTLM, `GSS-SPNEGO` or
  `DIGEST-MD5` bind, so this task also frames the SASL security layer, and depends on BL-328 (the
  protocol-neutral contract with `ISaslSecurityLayer`; it renames `MailLoginStep` to
  `SaslLoginStep`), BL-329 (NTLM sealing) and BL-326 (`DIGEST-MD5` layers). Sicily's `[9]`, `[10]`
  and `[11]` choices are answered here, mapped onto the `NTLM` exchange.
- **What was built (2026-09-30).** `LdapProtocolServer(IAuthenticationPolicy,
  ISaslAuthenticationPolicy, bool isTlsUpgradeAvailable = false)` and `LoadAsync` with the same
  three. The decoder reads Sicily's `[9]`/`[10]`/`[11]` (`LdapSicilyAuthentication`).
  `LdapSaslBindJudge` runs each SASL and Sicily bind through the policy's `ISaslExchange`
  (`CanCarrySecurityLayer: true`) and holds the exchange in progress; `LdapBindJudge` now answers
  every bind with an `LdapBindAnswer`. The session computes the root DSE's
  `supportedSASLMechanisms` and `StartTLS` per search, answers `StartTLS`, and frames every
  message after a bind with a layer as a 4-byte-length buffer the `ISaslSecurityLayer` protects.
  `ldaps` needs nothing of its own: the server tells TLS by `IConnection.TlsSession`, and
  `Surl.Console` wraps it in `ImplicitTlsSchemeServer` (BL-310), as the mail servers are.
- **Fixtures.** `ldap-ntlm-sealed`, `ldap-negotiate-sealed` and `ldap-digest-md5` copied byte for
  byte from `Surl.Authentication.UnitTests/Fixtures` (recorded by BL-329/BL-326 with the pinned
  win-x64 build); no new measurement was needed, and no upstream curl was run.
- **Decisions taken here (Decided by Claude under Stewart's delegation).** The ADR text could not
  be written in this task: BL-333 (Doing) holds `Documentation/Planning/Decisions`, so they are
  recorded here and BL-341 writes them into ADR-0072.
  1. `RefusedPlaintext` is `confidentialityRequired` (13), `SASL mechanism needs TLS or
     --allow-plaintext-auth` - the simple bind's code for the same rule, which curl reports as
     `Confidentiality Required`. `RefusedMechanism`, and every choice but simple, SASL and Sicily,
     is `authMethodNotSupported` (7), `authentication method not accepted` (the old `only simple
     binds are answered` is no longer true).
  2. Sicily `[9]` answers `success` with matched DN `NTLM` when the policy's SASL offer for the
     connection lists `NTLM` (the policy has no separate "is ntlm accepted" question); otherwise 7.
  3. A SASL bind continues the exchange in progress only when it names the same mechanism
     (case-insensitively, as the policy matches names); `[11]` continues only a Sicily exchange;
     every other bind - simple, another mechanism, `[9]`, `[10]`, a bad version - abandons it
     (RFC 4513 section 5.2.1.2 / RFC 4511 section 4.2.1).
  4. The security-layer note is `LDAP security layer: <mechanism>`: `ISaslSecurityLayer` carries
     no description, and widening the contract (Abstractions) was outside this task for a log line.
  5. A later bind's layer replaces the earlier one after that bind's response; a later bind with
     no layer keeps the installed one (nothing in RFC 4422 lets a client drop a layer).
  6. To discard bytes pipelined after `StartTLS` (ADR-0010), `LdapMessageFrameReader` reads a
     value in reads of at least 4096 bytes and holds the surplus; tag and length are still read
     byte by byte, so a message past `--max-message` is still refused before its value is read.
     `DiscardReadAhead` throws the surplus away, noted as `Discarded <n> bytes sent after
     StartTLS` (SMTP's wording).
  7. `StartTLS`'s `success` and `operationsError` answers carry its OID as `responseName`
     (RFC 4511 section 4.14.2); the no-certificate refusal is the unknown-operation answer, with
     none (ADR-0072). `StartTLS` on a TLS connection is `operationsError` with or without a
     certificate, so `ldaps` gets the right code whatever the console passes. A `requestValue` is
     ignored. The upgrade keeps the bind state (RFC 4513 does not reset it).
  8. A security-layer buffer the layer refuses, or one past the layer's maximum or
     `--max-message`, closes with no reply and a note; a malformed message inside the layer gets
     the Notice of Disconnection, protected.
- **Verification.** `dotnet build` clean (0 warnings). `dotnet build -warnaserror` fails only on
  NU1900 - nuget.org's vulnerability feed unreachable from this lane - for projects this task did
  not touch; no compiler or analyzer warning. Fast tests all green (493 in
  `Surl.Protocol.Ldap.UnitTests`). `Measure-CodeQuality.ps1 -Library
  Surl.Protocol.Ldap.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

- **Carried over (2026-09-30, lane 1).** Lane 5's push was refused, so lane 1 cherry-picked its
  code commit (`495b84b`) onto the current branch without conflicts and re-ran the gates: `dotnet
  build` clean (0 warnings), every fast test green (493 in `Surl.Protocol.Ldap.UnitTests`),
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ldap.UnitLibrary` 100% line, 100% branch,
  0 failing members, worst CRAP 10. Lane 5's follow-up BL-337 was refiled as BL-341 because
  another lane took BL-337 first.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Backlog. Lane 5 could not integrate: push kept being refused. The work is on branch factory/BL-309-lane-5-20260930-162236; start with git cherry-pick --no-commit factory/BL-309-lane-5-20260930-162236 and fix it.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Ldap answers WinLDAP's Sicily, GSS-SPNEGO and DIGEST-MD5 binds and any SASL mechanism through the SASL contract, frames the security layer, and answers StartTLS (and ldaps by TLS state)
