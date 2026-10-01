---
id: BL-330
title: Unwrap SPNEGO-wrapped NTLM in LDAP's Sicily and GSS-SPNEGO binds, with mechListMIC
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-329]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-330 — Unwrap SPNEGO-wrapped NTLM in LDAP's Sicily and GSS-SPNEGO binds, with mechListMIC

## Goal

An LDAP `NTLM` or `GSS-SPNEGO` exchange (`CanCarrySecurityLayer` true) whose token is SPNEGO
(`NegTokenInit` naming NTLMSSP, then `negTokenResp`s) runs the same NTLM handshake as a bare
token, answers with ADR-0040's `negTokenResp`s, handles the `mechListMIC` both ways, and leaves
the `NtlmSecurityLayer`'s sequence numbers where `WinLDAP` expects them for the first sealed
message.

## Context

- ADR-0072 decision 4: the Sicily `[10]` row ("bare NTLM, or SPNEGO by ADR-0040's rules: the
  reconnect sent SPNEGO here") and the `GSS-SPNEGO` row ("an SPNEGO token follows ADR-0040
  decision 3 ... with ADR-0040's `negTokenResp`s as `serverSaslCreds`, the last one on
  `success`"). BL-329 built bare NTLM only (`NtlmSaslExchange`, `NtlmSecurityLayer`) and refuses
  an SPNEGO token as a bad credential.
- Why it is its own task: with SPNEGO, Windows signs the `mechListMIC` with NTLM's
  `GSS_GetMIC`, which uses sequence number 0 in each direction ([MS-SPNG] 3.3.5.1, [MS-NLMP]
  3.4.4), so the sealed messages after it start at 1; the final `accept-completed` token must
  carry the server's `mechListMIC`, and the accepting `SaslLoginStep` needs a way to carry final
  data (BL-326 adds one for `DIGEST-MD5`'s `rspauth`; reuse it, or add it, re-checking `touches`
  if that means `Surl.Protocol.Abstractions.UnitLibrary`).
- Measured (ADR-0072): after a failed first connection `WinLDAP` reconnected with a version 2
  Sicily bind whose `[10]` held an SPNEGO-wrapped `NEGOTIATE_MESSAGE`. Record it with
  `Record-CurlExchange.ps1 -Ldap` (close the first connection with `-LdapReply 'BIND=CLOSE'`, then
  script the challenge) before pinning any byte.
- `SpnegoToken` and `NegotiateConnectionVerifier` already read and write the tokens for HTTP.

## Acceptance criteria

- [ ] A test replays a recorded `WinLDAP` SPNEGO-wrapped Sicily bind for `alice:secret`, accepts it,
      checks the client's `mechListMIC`, and unseals the first recorded sealed buffer to the
      plain search.
- [ ] Tests show the `negTokenResp`s sent (`accept-incomplete` with the challenge,
      `accept-completed` with the server's `mechListMIC`), and a bad `mechListMIC` refused.
- [ ] Bare NTLM (BL-329's fixtures) is unchanged.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for `Surl.Authentication.UnitLibrary`.

## Notes

- Filed by BL-329.

## Log

- 2026-09-30: Created.
