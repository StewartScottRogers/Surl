---
id: BL-329
title: Seal and sign NTLM and answer LDAP's NTLM and GSS-SPNEGO binds in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-328]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-049
created: 2026-09-30
completed: 2026-09-30
---
# BL-329 — Seal and sign NTLM and answer LDAP's NTLM and GSS-SPNEGO binds in Surl.Authentication

## Goal

`Surl.Authentication` answers an LDAP `NTLM` or `GSS-SPNEGO` SASL exchange
(`CanCarrySecurityLayer` true) with ADR-0072's LDAP `CHALLENGE_MESSAGE`, checks the
`AUTHENTICATE_MESSAGE` by ADR-0039's NTLMv2 check, and on acceptance returns an
`ISaslSecurityLayer` that seals or signs as MS-NLMP 3.4 says.

## Context

- Decision: ADR-0072 decision 4 (the LDAP challenge grants the sign, seal, key-exchange, 128-bit,
  56-bit and extended-session-security flags the `NEGOTIATE_MESSAGE` asked for; the layer per the
  `AUTHENTICATE_MESSAGE`'s flags; the protected bytes are the 16-byte signature then the sealed
  message; no unchecked NTLM under `--allow-anonymous`). ADR-0039 (the HTTP challenge, unchanged),
  ADR-0040 (bare NTLM or SPNEGO inside `GSS-SPNEGO`).
- MS-NLMP 3.4.4.2 (signature with extended session security), 3.4.5.1 to 3.4.5.3 (`KXKEY`,
  `SIGNKEY`, `SEALKEY`), 3.4.3 (sealing). RC4 is `Surl.Cryptography.Rc4.UnitLibrary`; MD5 and
  HMAC-MD5 are the BCL's. ADR-0002's table must allow the reference; if it does not, amend it in
  this task's ADR note.
- Fixtures: re-record with `Record-CurlExchange.ps1 -Ldap` (ADR-0072 "What upstream curl 8.21.0
  does": `--ntlm` and `--negotiate`, `-LdapReply "BIND=0|||<challenge hex>"` or
  `"BIND=14||<challenge hex>"`) - the `AUTHENTICATE_MESSAGE` and the first sealed buffer for a known
  password - and pin that unsealing it gives the plain search request curl sends after a simple
  bind.

## Acceptance criteria

- [x] A test unseals the recorded first buffer after `curl --ntlm -u alice:secret` with the recorded
      challenge and gets the base `searchRequest` bytes of ADR-0072's simple-bind transcript (message
      ID aside), and the same for `--negotiate`.
- [x] Tests show `Protect` then the client's view of `TryUnprotect` round-tripping both directions
      with sequence numbers from 0, a tampered signature and a replayed sequence number refused, and
      the signing-only and no-layer flag cases.
- [x] Tests show the HTTP NTLM and Negotiate challenges unchanged (ADR-0039, ADR-0040) and the LDAP
      challenge's flags as ADR-0072 decides; an unknown user under `--allow-anonymous` refused with
      ADR-0072's note.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for `Surl.Authentication.UnitLibrary`.

## Notes

- Filed by BL-284 (ADR-0072). BL-309 depends on it.
- Done: `NtlmSecurityLayer` ([MS-NLMP] 3.4 with extended session security: per-direction
  `SIGNKEY`/`SEALKEY`, RC4 handles kept running, sequence numbers from 0, the checksum
  RC4-encrypted under key exchange, signature then sealed message), `NtlmSessionKey`, the LDAP
  challenge grant (`NtlmChallengeMessage.ChooseFlags(flags, true)`), session-key export in
  `NtlmHandshake`, `CanCarrySecurityLayer` on `SaslExchangeContext`, and `GSS-SPNEGO` as
  `SaslMechanism.GssSpnego` running `NtlmSaslExchange`.
- Fixtures recorded 2026-09-30 with the pinned win-x64 build (`Fixtures/ldap-ntlm-sealed`,
  `Fixtures/ldap-negotiate-sealed`, README section "LDAP NTLM sealing"), scripting Surl's own LDAP
  challenge (server challenge `0123456789abcdef`). Both recorded first buffers unseal to ADR-0072's
  base search (message IDs 4 and 5). [MS-NLMP] 4.2.4.4's sealing example matches too.
- ADR-0002's table limits what protocol servers and horizontal libraries reference;
  `Surl.Authentication` is neither, so referencing `Surl.Cryptography.Rc4.UnitLibrary` needs no
  amendment (`ProtocolIsolationTests` passes).
- Decision (default taken): only a bare NTLM token is answered in LDAP's `NTLM` and `GSS-SPNEGO`
  binds; an SPNEGO-wrapped one is refused as a bad credential. Why: with SPNEGO, Windows signs
  `mechListMIC` with NTLM's `GSS_GetMIC` (sequence 0), which moves the sealed messages to 1, and
  the final `accept-completed` token needs the accepting step to carry final data. `WinLDAP`
  sends bare NTLM on its first try (measured). Filed as BL-330.
- Decision (default taken): a login asking for signing or sealing without extended session
  security is refused with `NTLM signing and sealing need extended session security`. Why:
  `WinLDAP` always negotiates it (`E2888235`), and the non-ESS form (CRC32, `RandomPad`) serves
  no measured client.
- Decision (default taken): under key exchange, an `EncryptedRandomSessionKey` that is not 16
  bytes is refused. `MaximumProtectedBytes` is `int.MaxValue`: NTLM has no buffer limit of its
  own, and the server's `--max-message` bounds the buffer (ADR-0072 decision 6).
- `--allow-anonymous` where a layer can follow: a user with no account (or an unreadable
  message) is refused with `the security layer needs the account's password`; a known user is
  checked as usual. Mail `NTLM` keeps ADR-0049's unchecked acceptance and never gets a layer.
- `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary`: 100% line, 100% branch,
  431 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. LDAP NTLM and GSS-SPNEGO binds get the LDAP challenge and an NTLM sealing/signing security layer; WinLDAP's recorded sealed searches unseal
