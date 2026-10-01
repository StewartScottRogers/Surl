---
id: BL-335
title: Correct ADR-0072's SPNEGO reconnect claim and record BL-330's mechListMIC decisions
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-330]
touches: [Documentation/Planning/Decisions]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-335 — Correct ADR-0072's SPNEGO reconnect claim and record BL-330's mechListMIC decisions

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

- [ ] ADR-0072's measurement bullet and decision 4's Sicily row no longer say the reconnect sent
      SPNEGO; they say `WinLDAP` sends bare NTLM in every measured configuration (BL-330).
- [ ] A new ADR, marked "Decided by Claude under Stewart's delegation", records BL-330's
      SPNEGO and `mechListMIC` decisions above and why, and `Documentation/Planning/Decisions/README.md`
      lists it.

## Notes

- Filed by BL-330.

## Log

- 2026-09-30: Created.
