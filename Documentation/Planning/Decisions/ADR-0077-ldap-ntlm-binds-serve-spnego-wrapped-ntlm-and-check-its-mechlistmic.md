# ADR-0077 — LDAP NTLM binds serve SPNEGO-wrapped NTLM and check its `mechListMIC`

- **Status:** Accepted
- **Date:** 2026-10-01
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-330, which made and built the decisions; recorded by BL-336, since BL-330's lane could
  not write in this folder while another lane held it.
- **Amends:** [ADR-0072](ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md)
  decision 4's Sicily `[10]` and SASL `GSS-SPNEGO` rows, for an SPNEGO-wrapped NTLM token; and,
  for LDAP only, [ADR-0040](ADR-0040-http-negotiate-carrying-ntlm-bare-or-in-spnego.md) decision 4
  ("No `mechListMIC`"), which HTTP Negotiate keeps.

## Context

ADR-0072 decision 4 serves an NTLM bind, over Sicily or SASL `GSS-SPNEGO`, bare or wrapped in
SPNEGO by ADR-0040's rules, and its measurement said `WinLDAP`'s reconnect after a failed sealed
bind wrapped the NTLM token in SPNEGO. BL-330 measured again on 2026-09-30, with the win-x64
reference build through `Record-CurlExchange.ps1 -Ldap`, in ten configurations: `--ntlm` and
`--negotiate`; a bind closed, refused with 49, or answered with BL-329's scripted challenge
granting sign and seal (`35828AE0`) or not (`058288E2`); a root DSE listing `GSS-SPNEGO` and
`NTLM`, `NTLM` only, or nothing; `127.0.0.1` and `localhost`. Every Sicily `[10]` and `[11]`
credential and every `GSS-SPNEGO` credential was a bare `NTLMSSP` message, the version 2 Sicily
retry included, as BL-329's `Surl.Authentication.UnitTests/Fixtures/ldap-negotiate-sealed`
transcript also shows. So no pinned upstream curl was measured to send SPNEGO-wrapped NTLM to an
LDAP server; ADR-0072 is corrected to say so.

Unlike HTTP Negotiate, an LDAP NTLM bind can be followed by a security layer (ADR-0072 decision
6), so NTLM there does give integrity protection, and RFC 4178 section 5 then expects the
`mechListMIC` that ADR-0040 decision 4 skips.

## Decision

1. **SPNEGO-wrapped NTLM is still served** in a Sicily `[10]`/`[11]` or `GSS-SPNEGO` bind, by
   ADR-0040 decision 3 with NTLM the mechanism selected: a `NegTokenInit` must list NTLM; its
   optimistic token is NTLM's first message only when NTLM is the client's first mechanism,
   otherwise the reply only names NTLM (`accept-incomplete`, `supportedMech` NTLM) and the client
   starts NTLM in its next token. Each NTLM message travels in a `negTokenResp` both ways. A token
   that is not SPNEGO naming NTLM is refused as an unreadable NTLM message is. Mail `NTLM` never
   unwraps (ADR-0049).
2. **The `mechListMIC` is NTLM's `GSS_GetMIC`** over the DER of the client's `MechTypeList`, with
   sequence number 0. After it, each direction's RC4 handle is keyed afresh from the sealing key
   and its sequence number is not reset, so the first sealed message in each direction carries
   sequence number 1.
3. **A wrong `mechListMIC` is refused** (`NtlmSaslExchange.WrongMechListMicNote`). **A missing
   one is refused only when NTLM was not the client's first mechanism**
   (`MissingMechListMicNote`); otherwise success is answered with an `accept-completed`
   `negTokenResp` alone. A `mechListMIC` from a client that did not negotiate extended session
   security is refused (`NoExtendedSessionSecurityNote`).
4. **Surl sends its own `mechListMIC`** in the `accept-completed` `negTokenResp` that goes with
   `success` only when the client sent one.
5. **HTTP Negotiate is unchanged**: `SpnegoToken.ReadNegTokenResp` now returns the
   `mechListMIC` beside the response token (`SpnegoNegTokenResp`), and HTTP Negotiate still
   ignores it (ADR-0040 decision 4).

## Why

- **Serve it although `WinLDAP` was not measured sending it:** ADR-0072 decision 4 already lists
  it, `GSS-SPNEGO` is by name an SPNEGO mechanism (RFC 4178), and the standing rule is that
  nothing a complete server-side mate needs is left out. It is tested as HTTP Negotiate's SPNEGO
  is, with the recorded NTLM messages wrapped as RFC 4178 lays them out.
- **Sequence number 0, then fresh RC4 handles:** [MS-SPNG] section 3.1.5.1, "NTLM RC4 Key State
  for MechListMIC and First Signed Message", restores the RC4 handle around the MIC; Samba's
  `gensec_spnego` does the same by calling `gensec_may_reset_crypto(..., reset_full =
  !done_mic_check)`, which after a MIC check resets the RC4 state and keeps the sequence numbers.
  BL-330's test shows the sealed body that follows equals `WinLDAP`'s recorded one, which only a
  fresh handle produces.
- **When a missing MIC is refused:** RFC 4178 section 5 requires the `mechListMIC` when the
  selected mechanism is not the initiator's first choice, since only then could a downgrade have
  happened; with NTLM first, nothing was negotiated away.

## Alternatives considered

- **Refuse SPNEGO-wrapped NTLM in LDAP binds**, as unmeasured. Rejected: it is a valid
  `GSS-SPNEGO` exchange, ADR-0072 already promises it, and leaving it out is what the standing
  rule forbids.
- **Skip the `mechListMIC`, as HTTP Negotiate does.** Rejected: with a security layer NTLM gives
  integrity protection, and RFC 4178 section 5 then has the MIC protect the mechanism list.
- **Keep the RC4 handle running through the MIC** (the MIC consuming keystream). Rejected: it
  contradicts [MS-SPNG] 3.1.5.1 and would make every sealed message after the bind unreadable to
  a Windows client.

## Consequences

- `NtlmSaslExchange` in `Surl.Authentication` serves the SPNEGO steps and the `mechListMIC`;
  `NtlmSecurityLayer` signs and verifies it and re-keys its RC4 handles.
- ADR-0072's measurement and decision 4's Sicily `[10]` row now say `WinLDAP` sends bare NTLM in
  every measured configuration.
- SPNEGO-wrapped NTLM in LDAP is proved by unit tests only, since no pinned upstream curl build
  was measured sending it.
