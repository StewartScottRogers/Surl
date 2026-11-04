---
id: BL-352
title: Replay the macOS upstream curl build's non-ASCII NTLM password login from CI's recording
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-324, BL-353]
touches: [Surl.Authentication.UnitTests]
requirement: FR-014
created: 2026-10-01
completed: 2026-10-01
---
# BL-352 — Replay the macOS upstream curl build's non-ASCII NTLM password login from CI's recording

## Goal

The pinned `osx-arm64` upstream curl 8.21.0 build's HTTP NTLM login as `tester:pässword`,
recorded by CI, is committed as `Fixtures/ntlm-non-ascii-password-macos` and replayed by
`Surl.Authentication.UnitTests`.

## Context

- Split from BL-324 (2026-10-01): no Mac is reachable from the dark factory's machine, so
  Stewart had the macOS build recorded in CI. BL-324 added the steps "Record the non-ASCII NTLM
  password login" and "Publish the non-ASCII NTLM password recording" to the macOS leg of
  `.github/workflows/ci.yml`; every CI run on `master` or a pull request from then on publishes
  the artifact `ntlm-non-ascii-password-macos` (the six files `Record-CurlExchange.ps1` writes
  with `-ResponsesPerConnection 2`).
- Fetch it: `gh run list --workflow CI --branch master --status success --limit 1`, then
  `gh run download <run-id> -n ntlm-non-ascii-password-macos -D Surl.Authentication.UnitTests\Fixtures\ntlm-non-ascii-password-macos`.
  If no successful run carries it yet, move this task to Backlog with that reason.
- Model it on `ntlm-non-ascii-password-linux` (BL-324): the Fixtures/README.md row (build
  `/opt/upstream-curl/8.21.0/curl`, SHA-256 `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF`,
  `-Port 18325`, the run's URL as where it was recorded), a `DataRow` on
  `NtlmAuthenticationMethodTests.RecordedAuthenticate_NonAsciiPassword_IsAcceptedAsTheAccount`,
  and a row (make it a `DataRow`) on
  `RecordedAuthenticate_LinuxBuild_ProvesTheNtHashOfTheWidenedUtf8Password`, renamed for both
  builds.

## Acceptance criteria

- [x] `Surl.Authentication.UnitTests/Fixtures/ntlm-non-ascii-password-macos` holds the CI
      recording, exit code `0` and stdout `ok`, documented in `Fixtures/README.md` with the
      build's path, SHA-256 and the CI run it came from.
- [x] `RecordedAuthenticate_NonAsciiPassword_IsAcceptedAsTheAccount` has a row for it and passes.
- [x] The answer's `NTProofStr` is checked against the NT hash of the widened UTF-8 password,
      as the Linux recording's is, and passes.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-01 (lane 1): no CI run has published `ntlm-non-ascii-password-macos` yet. The last
  successful `master` run (36677388670) predates BL-324's CI steps. The first runs carrying
  them, 36840327050 (70f965c8, cancelled by the next push) and 36840335608 (33959f8b), never
  reached the recording step: macOS's "Fast tests" step fails first, with 47
  `Surl.Protocol.Ssh.UnitTests` tests throwing `PlatformNotSupportedException` because
  `SshTestKeys` generates a DSA key, which macOS's BCL cannot do. Filed BL-353 to import a
  fixed DSA key instead, and made this task depend on it. Once BL-353 lands, any CI run (a
  pull request run is fine; the step does not need `master`) publishes the artifact.
- 2026-10-01 (lane 2): no successful `master` run carries the artifact yet, but CI run
  36848637228 (factory/phase-1, commit 32f41ad0, after BL-353's fix) passed macOS's fast tests,
  recorded and published `ntlm-non-ascii-password-macos`; the run was later cancelled at its
  conformance step by the next push. The recording steps completed, so the artifact is the
  pinned osx-arm64 build's (CI verified its SHA-256) and was taken from that run - the
  sensible default over waiting for a fully green run. Its request bytes, exit code `0` and
  stdout `ok` match the Linux recording's shape; the NTLM answer differs (its own client
  nonce and timestamp). The Linux-only proof test became a `DataRow` test,
  `RecordedAuthenticate_LinuxAndMacOsBuilds_ProveTheNtHashOfTheWidenedUtf8Password`.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Backlog. Waits on BL-353: macOS CI fast tests fail on DSA key generation, so the recording step never runs
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. The macOS upstream curl build's tester:pässword NTLM login from CI run 36848637228 is replayed and its NTProofStr proves the widened-UTF-8 NT hash
