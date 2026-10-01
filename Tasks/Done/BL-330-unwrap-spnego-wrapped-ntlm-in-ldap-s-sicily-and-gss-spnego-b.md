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
completed: 2026-09-30
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

- [x] A test replays a recorded `WinLDAP` SPNEGO-wrapped Sicily bind for `alice:secret`, accepts it,
      checks the client's `mechListMIC`, and unseals the first recorded sealed buffer to the
      plain search.
      *Met as far as upstream curl allows:* `WinLDAP` never sends SPNEGO here (Notes), so
      `LdapSpnegoNtlmSaslMechanismTests.WinLdapsRecordedBind_WrappedInSpnego_...` wraps the
      recorded Sicily and `GSS-SPNEGO` messages in SPNEGO, checks a `mechListMIC` made with the
      recording's own session key, and shows the first search sealed after it is `WinLDAP`'s
      recorded ciphertext byte for byte, signed with sequence number 1, which the server unseals.
- [x] Tests show the `negTokenResp`s sent (`accept-incomplete` with the challenge,
      `accept-completed` with the server's `mechListMIC`), and a bad `mechListMIC` refused.
- [x] Bare NTLM (BL-329's fixtures) is unchanged.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for `Surl.Authentication.UnitLibrary`.

## Notes

- Filed by BL-329.
- Measured (2026-09-30, win-x64 reference build, `Record-CurlExchange.ps1 -Ldap`, port 18330,
  outputs kept out of the repository): ten configurations - `--ntlm` and `--negotiate`;
  `BIND=CLOSE`, `BIND=49`, and BL-329's scripted challenge with sign/seal granted (`35828AE0`) and
  not (`058288E2`); root DSE listing `GSS-SPNEGO`+`NTLM`, `NTLM` only, or nothing; `127.0.0.1` and
  `localhost`. Every Sicily `[10]`/`[11]` and every `GSS-SPNEGO` credential was bare `NTLMSSP`,
  including the version 2 Sicily retry after a failed sealed bind. ADR-0072's "the reconnect sent
  SPNEGO here" did not reproduce; BL-336 corrects the ADR. No new fixture was committed: the
  recordings add nothing to BL-329's.
- Decision (taken under Stewart's delegation): serve SPNEGO-wrapped NTLM anyway, as ADR-0072
  decision 4's rows say and "nothing is left out" asks, tested like HTTP Negotiate's SPNEGO
  (recorded NTLM messages wrapped as RFC 4178 lays them out). Mail `NTLM` never unwraps.
- Decision: the `mechListMIC` is NTLM's `GSS_GetMIC` over the client's `MechTypeList` DER, with
  sequence number 0; afterwards each direction's RC4 handle is keyed afresh and its sequence
  number is not reset, so the first sealed message carries 1. Why: [MS-SPNG] 3.1.5.1 ("NTLM RC4
  Key State for MechListMIC and First Signed Message") restores the handle around the MIC, and
  Samba's `gensec_spnego` calls `gensec_may_reset_crypto(..., reset_full = !done_mic_check)`,
  which after a MIC check resets the RC4 state but keeps the sequence numbers. The test shows the
  sealed body then equals `WinLDAP`'s recorded one, which only a fresh handle produces.
- Decision: a wrong `mechListMIC` is refused (`WrongMechListMicNote`); a missing one is refused
  only when NTLM was not the client's first mechanism (RFC 4178 section 5,
  `MissingMechListMicNote`), and otherwise answered with `accept-completed` alone; a
  `mechListMIC` without extended session security is refused (`NoExtendedSessionSecurityNote`).
  The server sends its own `mechListMIC` only when the client sent one.
- `SpnegoToken.ReadNegTokenResp` now returns `SpnegoNegTokenResp` (response token and
  `mechListMIC`); HTTP Negotiate still ignores the MIC, unchanged.
- No ADR written here: BL-268 (Doing) touches `Documentation/Planning/Decisions`. Filed BL-336
  (docs, depends on BL-330) to correct ADR-0072 and record these decisions.
- `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary`: 100% line, 100% branch,
  469 members, 0 failing, worst CRAP 10. Authentication tests: 905 (was 885).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. LDAP NTLM and GSS-SPNEGO binds unwrap SPNEGO-wrapped NTLM, check and send the mechListMIC, and seal from sequence number 1
