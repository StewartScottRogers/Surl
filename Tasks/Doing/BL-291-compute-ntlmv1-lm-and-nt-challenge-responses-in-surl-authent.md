---
id: BL-291
title: Compute NTLMv1 LM and NT challenge responses in Surl.Authentication
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-052
created: 2026-09-30
completed:
---
# BL-291 — Compute NTLMv1 LM and NT challenge responses in Surl.Authentication

## Goal

`Surl.Authentication.UnitLibrary` computes the NTLMv1 LM and NT challenge responses - the 24-byte
responses upstream curl's SMB client sends in its session setup - from an account's password and an
8-byte server challenge, so the SMB login check (BL-295) can compare them.

## Context

- What curl sends (`lib/smb.c` at tag `curl-8_21_0`, read 2026-09-30): `Curl_ntlm_core_mk_lm_hash`
  then `Curl_ntlm_core_lm_resp` over the server's challenge for the LM response, and
  `Curl_ntlm_core_mk_nt_hash` then the same `Curl_ntlm_core_lm_resp` for the NT response: NTLMv1
  without extended session security ([MS-NLMP] section 3.3.1).
- What to build, beside `NtlmV2Calculation.cs`: `LMOWFv1` (the password upper-cased in the OEM code
  page, padded or cut to 14 bytes, two 7-byte halves as DES keys encrypting `KGS!@#$%`),
  `NTOWFv1` (MD4 of the UTF-16LE password, with the existing `Surl.Cryptography` `Md4`), and
  `DESL` (the 16-byte hash padded to 21 bytes, three DES-ECB encryptions of the challenge). DES-ECB
  from the BCL's `System.Security.Cryptography.DES`, which .NET documents as supported on Windows,
  Linux and macOS (learn.microsoft.com, "Cross-platform cryptography", 2026-08-03), so no hand-built
  primitive is needed; the 7-byte to 8-byte key expansion with parity is hand-written.
- Decide within this task, and say so in its Notes: which OEM code page `LMOWFv1` uses for a
  non-ASCII password (the one the pinned build's `Curl_ntlm_core_mk_lm_hash` uses - read its source
  at the tag, do not guess), and the behaviour for a password over 14 bytes (the LM response is then
  meaningless; say what the calculation returns).
- Only the calculation: whether and how an SMB login accepts NTLMv1, and the contract that asks for
  it, are BL-283's ADR and BL-294/BL-295. Nothing here is reachable from a server yet.
- Test vectors: [MS-NLMP] section 4.2.2 ("NTLM v1 Authentication"): user `User`, domain `Domain`,
  password `Password`, server challenge `0123456789abcdef`, and its LMOWFv1, NTOWFv1, LMv1 response
  and NTLMv1 response values, taken from the specification.
- Constraints: complexity at most 10 per method; secrets compared later with the existing
  `CryptographicSecretComparer`, not here.

## Acceptance criteria

- [ ] Tests in `Surl.Authentication.UnitTests` reproduce [MS-NLMP] 4.2.2's LMOWFv1, NTOWFv1, LMv1
      response and NTLMv1 response byte for byte, and pass on Windows, Linux and macOS (no
      `OSCondition`).
- [ ] Tests cover an empty password, a 14-byte and a longer password, and a non-ASCII password.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Authentication.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
