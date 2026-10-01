---
id: BL-324
title: Record a non-ASCII NTLM password login from the Linux and macOS upstream curl builds
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Authentication.UnitTests]
requirement: FR-014
created: 2026-09-30
completed:
---
# BL-324 — Record a non-ASCII NTLM password login from the Linux and macOS upstream curl builds

## Goal

A recorded HTTP NTLM login with the password `pässword` from the pinned `linux-x64` and
`osx-arm64` upstream curl 8.21.0 builds is replayed by a test in `Surl.Authentication.UnitTests`
and accepted.

## Context

- Found in BL-321 (2026-09-30). `NtlmPasswordHashes` keeps, for every account, the NT hash of the
  password's UTF-8 bytes each widened to 16 bits, which is what upstream curl's own NTLM code
  computes (`Curl_ntlm_core_mk_nt_hash` in `lib/curl_ntlm_core.c` at `curl-8_21_0`) and so what
  the Linux and macOS builds (OpenSSL, no SSPI) should send. BL-321 could record only the two
  Windows builds: the Linux and macOS builds are not installed on the dark factory's machine
  (ADR-0016: CI downloads them). Today the widened-UTF-8 case is covered only by a synthesized
  message (`NtlmAuthenticationMethodTests.Authenticate_NtHashOfTheWidenedUtf8Password_IsAccepted`).
- Record as BL-321 did (`Surl.Authentication.UnitTests/Fixtures/README.md`, "Non-ASCII NTLM
  password"): `-Port <p> -ResponsesPerConnection 2 -Response <ntlm401>,<ok>`,
  `-CurlArgs '-sS','--ntlm','-u','tester:pässword','http://127.0.0.1:<p>/x'`, on a machine where
  the pinned build is at its `defaultPath` (or in CI, where ADR-0016 installs it). If no such
  machine is reachable without a new download, move this task to Blocked for Stewart.

## Acceptance criteria

- [ ] `Fixtures/ntlm-non-ascii-password-linux` and `Fixtures/ntlm-non-ascii-password-macos` hold
      the recordings, documented in `Fixtures/README.md` with the build's path and SHA-256.
- [ ] `NtlmAuthenticationMethodTests.RecordedAuthenticate_NonAsciiPassword_IsAcceptedAsTheAccount`
      has a row for each and passes.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
