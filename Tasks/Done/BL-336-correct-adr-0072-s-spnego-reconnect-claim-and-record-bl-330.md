---
id: BL-336
title: Correct ADR-0072's SPNEGO reconnect claim and record BL-330's mechListMIC decisions
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-330]
touches: [Documentation/Planning/Decisions]
requirement: FR-049
created: 2026-09-30
completed: 2026-10-01
---
# BL-336 — Correct ADR-0072's SPNEGO reconnect claim and record BL-330's mechListMIC decisions

## Goal

ADR-0072 states only what pinned upstream curl was measured to send in LDAP NTLM binds, and an
ADR marked "Decided by Claude under Stewart's delegation" records how BL-330 handles
SPNEGO-wrapped NTLM and its `mechListMIC`.

## Context

- ADR-0072 says `WinLDAP`'s reconnect after a failed sealed bind sent "Sicily with the NTLM token
  wrapped in SPNEGO in a version 2 bind" (the measurement bullet near line 136, and decision 4's
  Sicily `[10]` row). BL-330 could not reproduce it: across ten `Record-CurlExchange.ps1 -Ldap`
  runs with the win-x64 reference build (Sicily and `GSS-SPNEGO`; `BIND=CLOSE`, `BIND=49`, the
  scripted challenge with and without sign/seal granted; root DSE listing `GSS-SPNEGO` and `NTLM`,
  `NTLM` only, or nothing; `127.0.0.1` and `localhost`), every `[10]`, `[11]` and `GSS-SPNEGO`
  credential was a bare `NTLMSSP` message, including the version 2 Sicily retry. BL-329's own
  `Fixtures/ldap-negotiate-sealed` transcript shows the same bare version 2 bind.
- BL-330's decisions (see its Notes): SPNEGO-wrapped NTLM is still served where a security layer
  can follow, by ADR-0040 decision 3; the `mechListMIC` is NTLM's `GSS_GetMIC` with sequence
  number 0, and each RC4 handle is keyed afresh after it ([MS-SPNG] 3.1.5.1, as Samba's
  `gensec_spnego` does with `reset_full = false`), so the first sealed message carries 1; a wrong
  `mechListMIC` is refused, a missing one only when NTLM was not the client's first mechanism.
- Not done in BL-330 because BL-268 (Doing) touched `Documentation/Planning/Decisions`.

## Acceptance criteria

- [x] ADR-0072's measurement bullet and decision 4's Sicily row no longer say the reconnect sent
      SPNEGO; they say `WinLDAP` sends bare NTLM in every measured configuration (BL-330).
- [x] A new ADR, marked "Decided by Claude under Stewart's delegation", records BL-330's
      SPNEGO and `mechListMIC` decisions above and why, and `Documentation/Planning/Decisions/README.md`
      lists it.

## Notes

- Filed by BL-330.
- ADR-0072: the measurement bullet now says the reconnect was a version 2 Sicily bind holding
  bare NTLM, with a "Corrected by BL-336" note citing BL-330's ten configurations and BL-329's
  fixture; decision 4's Sicily `[10]` row says bare NTLM is what `WinLDAP` sends in every measured
  configuration and points SPNEGO at ADR-0077; the header's **Amended:** line lists BL-336.
- New ADR-0077 (decided by Claude under Stewart's delegation, in BL-330) records serving
  SPNEGO-wrapped NTLM, the `mechListMIC` (sequence 0, fresh RC4 handles, first sealed message 1),
  the refusal rules and HTTP Negotiate unchanged; each claim checked against
  `NtlmSaslExchange` and `NtlmSecurityLayer`. Listed in `Decisions/README.md`.
- Took the next ADR number, 0077; if another lane lands an ADR-0077 first, renumber on rebase.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. ADR-0072 says WinLDAP sends bare NTLM in every measured configuration, and ADR-0077 records BL-330's SPNEGO and mechListMIC decisions
