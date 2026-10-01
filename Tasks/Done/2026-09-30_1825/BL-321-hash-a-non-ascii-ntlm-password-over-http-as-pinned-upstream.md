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
completed: 2026-09-30
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

- [x] A recorded fixture from each pinned NTLM-capable upstream curl build logs in over HTTP NTLM
      with the password `pässword`, and a test in `Surl.Authentication.UnitTests` replays it and
      is accepted. (Both Windows builds on this machine, from the command line and a `-K` file:
      `NtlmAuthenticationMethodTests.RecordedAuthenticate_NonAsciiPassword_IsAcceptedAsTheAccount`.
      The Linux and macOS builds are not installed here; their hash is tested synthesized and
      recording them is BL-324.)
- [x] An ASCII-password NTLM fixture that passes today still passes.
- [x] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1` reports no
      failing member for `Surl.Authentication.UnitLibrary`.

## Notes

- Measured (2026-09-30, `pässword`, fixed challenge `0123456789abcdef`; Fixtures/README.md,
  "Non-ASCII NTLM password"): the static-curl Windows build (Unicode, SSPI) proves
  `MD4(UTF-16LE(password))`; the Windows reference build (SSPI with an ANSI identity) proves
  neither expected form but `MD4(UTF-16LE(cp437(cp1252 bytes)))` from the command line and
  `MD4(UTF-16LE(cp437(UTF-8 bytes)))` from a `-K` file: SSPI reads the ANSI bytes in the OEM
  code page. The Linux and macOS builds use `lib/curl_ntlm_core.c`, which widens each UTF-8 byte.
- Decision (Claude, under Stewart's delegation): every account keeps all four hashes
  (`NtlmPasswordHashes`) and `NtlmHandshake` accepts an answer proving any of them, checking all
  four every time; the dummy keeps four random hashes, so the work stays constant. Code pages
  1252 and 437 are fixed (US-English Windows); the encodings come from
  `CodePagesEncodingProvider.Instance`, in the shared framework, so no package. Each extra hash
  is a deterministic form of the same password, admitting only a near-guess of it.
- The ADR could not be written here: `Documentation/Planning/Decisions` is held by BL-285 in
  Doing and is outside this task's `touches`. Filed BL-325 to record it, with the decision text.
- The Linux and macOS builds are not on this machine (CI downloads them, ADR-0016) and a download
  needs Stewart, so the widened-UTF-8 hash is tested with a synthesized answer; BL-324 records the
  real ones.
- SASL `NTLM` and Negotiate carrying NTLM share `NtlmHandshake`, so they get the same hashes.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. NTLM accounts keep four NT hashes, so a non-ASCII password logs in from both pinned Windows builds (and curl's own widened-UTF-8 form); filed BL-324, BL-325
