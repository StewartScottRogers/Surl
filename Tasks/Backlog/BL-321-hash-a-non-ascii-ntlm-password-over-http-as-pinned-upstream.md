---
id: BL-321
title: Hash a non-ASCII NTLM password over HTTP as pinned upstream curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-014
created: 2026-09-30
completed:
---
# BL-321 — Hash a non-ASCII NTLM password over HTTP as pinned upstream curl does

## Goal

An HTTP NTLM (and Negotiate-carrying-NTLM, and SASL `NTLM`) login with a non-ASCII password
succeeds against every pinned upstream curl build, because the account's NT hash is computed from
the password the way that build computes it.

## Context

- Found in BL-291 (2026-09-30). `AccountBook` keeps each account's NT hash from
  `NtlmV2Calculation.ComputeNtHash`, which is `MD4(UTF-16LE(password))`. Upstream curl's own NTLM
  code (`lib/curl_ntlm_core.c` at `curl-8_21_0`, `Curl_ntlm_core_mk_nt_hash`) instead widens each
  byte of the password it was given (UTF-8) to 16 bits (`ascii_to_unicode_le`), so for a password
  with a non-ASCII character the two hashes differ and the login would be refused. The two agree
  for ASCII passwords, which is all the recorded fixtures use.
- A Windows build that uses SSPI for NTLM (`USE_WINDOWS_SSPI`) hands the password to Windows, which
  may hash the true UTF-16LE form: the answer can differ by build. Measure each pinned build in
  `UpstreamCurlBuilds.json` that supports NTLM with `Record-CurlExchange.ps1` and a non-ASCII
  password before choosing; if builds disagree, decide (ADR, "Decided by Claude under Stewart's
  delegation") whether an account keeps both hashes and accepts either.
- `NtlmV1Calculation.ComputeNtHashOfWidenedUtf8` (BL-291) already computes curl's form.

## Acceptance criteria

- [ ] A recorded fixture from each pinned NTLM-capable upstream curl build logs in over HTTP NTLM
      with the password `pässword`, and a test in `Surl.Authentication.UnitTests` replays it and
      is accepted.
- [ ] An ASCII-password NTLM fixture that passes today still passes.
- [ ] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1` reports no
      failing member for `Surl.Authentication.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
