---
id: BL-291
title: Compute NTLMv1 LM and NT challenge responses in Surl.Authentication
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Cryptography.UnitLibrary, Surl.Cryptography.UnitTests]
requirement: FR-052
created: 2026-09-30
completed: 2026-09-30
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

- [x] Tests in `Surl.Authentication.UnitTests` reproduce [MS-NLMP] 4.2.2's LMOWFv1, NTOWFv1, LMv1
      response and NTLMv1 response byte for byte, and pass on Windows, Linux and macOS (no
      `OSCondition`).
- [x] Tests cover an empty password, a 14-byte and a longer password, and a non-ASCII password.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Authentication.UnitLibrary`.

## Notes

- **DES is hand-built, in `Surl.Cryptography` (`Des.EncryptBlock`).** The Context assumed the
  BCL's `System.Security.Cryptography.DES`, but it refuses weak and semi-weak keys (`Key` and
  `SetKey` both throw "Specified key is a known weak key", measured 2026-09-30), and NTLMv1 needs
  them: an empty password's LM key is the all-zero weak key. So `Surl.Cryptography.UnitLibrary`
  and its tests were added to `touches` (no task in Doing names them). Tested against "The DES
  Algorithm Illustrated", NIST SP 800-17 table A.1's weak-key vectors, and the BCL's DES-ECB over
  200 random keys it accepts. Encryption only; NTLMv1 never decrypts.
- **Code page for a non-ASCII password (decided from the source, not guessed).** Read at tag
  `curl-8_21_0`: `Curl_ntlm_core_mk_lm_hash` takes `CURLMIN(strlen(password), 14)` bytes and
  upper-cases them with `Curl_strntoupper`, whose `touppermap` in `lib/strcase.c` maps only
  `a`-`z`. There is no OEM code page: curl uses the password's bytes as given, which are UTF-8
  (argv on Linux and macOS; `wmain` converts to UTF-8 in `_UNICODE` Windows builds). So
  `ComputeLmHash` takes the UTF-8 bytes, upper-cases only ASCII letters and keeps non-ASCII bytes
  as they are.
- **NT hash follows curl, not the specification, for non-ASCII.** `Curl_ntlm_core_mk_nt_hash`
  widens each byte with `ascii_to_unicode_le`, so "é" is hashed as `C3 00 A9 00`, not UTF-16LE
  `E9 00`. Hence the name `ComputeNtHashOfWidenedUtf8`. For ASCII it is exactly NTOWFv1.
  `lib/smb.c` calls this code directly (never SSPI), so it is what curl's SMB client sends.
- **Password over 14 bytes:** LM uses its first 14 bytes, as curl does, so every password sharing
  them has one LM hash (and LM response); the NT hash still covers the whole password.
- The 7-to-8-byte key expansion mirrors curl's `extend_key_56_to_64`; parity is not set because
  DES ignores the parity bits (tested: flipping them gives the same block).
- Follow-up filed: BL-321. The HTTP NTLM account hash (`NtlmV2Calculation.ComputeNtHash`) uses
  true UTF-16LE, which disagrees with curl's widened bytes for a non-ASCII password.
- Verified: [MS-NLMP] 4.2.2's LMOWFv1, NTOWFv1, LMv1 and NTLMv1 responses byte for byte;
  `dotnet build` clean; fast tests green (Authentication 774, Cryptography 25);
  `Measure-CodeQuality.ps1` 34 of 34 libraries at 100% line and branch, 0 failing members.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. NTLMv1 LM and NT responses computed as upstream curl's SMB client does, over a hand-built DES; MS-NLMP 4.2.2 reproduced
