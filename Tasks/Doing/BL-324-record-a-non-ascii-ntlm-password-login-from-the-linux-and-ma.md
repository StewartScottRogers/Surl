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

- 2026-09-30 (lane 2): neither pinned build is reachable without a new download. The dark
  factory's machine is Windows 11; its WSL Ubuntu has nothing at `/opt/upstream-curl/8.21.0/`
  and no `curl-linux-*` archive exists on the machine or in WSL; no macOS machine is reachable.
  CI installs both builds (ADR-0016), but a lane may not push or run CI, and recording there
  needs a workflow step outside this task's `touches`. Per the task's Context, Blocked for
  Stewart: either approve installing the pinned `linux-x64` build in WSL (and record macOS on a
  Mac), or have the recordings made in CI.
- 2026-10-01: Stewart answered "download it" / "yes": download the pinned `linux-x64`
  static-curl 8.21.0 build into WSL at its `defaultPath` from `UpstreamCurlBuilds.json`, verify
  its SHA-256 there, and record the Linux fixture with it. No Mac is reachable, so record
  `osx-arm64` in CI (ADR-0016 installs it there): widen `touches` to the workflow step that does
  it, as the dark factory's rules allow.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Blocked. Stewart: may I download the pinned linux-x64 static-curl 8.21.0 build into WSL (and record osx-arm64 on a Mac or in CI), since neither is on this machine?
- 2026-10-01: Blocked -> Backlog. Stewart, 2026-10-01: download it - yes. Approved downloading the pinned linux-x64 static-curl 8.21.0 build into WSL to record the Linux fixture; record osx-arm64 in CI.
- 2026-10-01: Backlog -> Doing.
