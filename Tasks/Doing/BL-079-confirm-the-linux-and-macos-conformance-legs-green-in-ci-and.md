---
id: BL-079
title: Confirm the Linux and macOS conformance legs green in CI and correct the pins from the builds' own --version
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-028]
touches: [UpstreamCurlBuilds.json, .github/workflows/ci.yml, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-079 — Confirm the Linux and macOS conformance legs green in CI and correct the pins from the builds' own --version

## Goal

A `CI` run that includes BL-028's commits is green on Windows, Linux and macOS, its
Linux and macOS `Conformance tests` steps report the `Surl.Conformance` tests passed (not
skipped or inconclusive), and the `linux-x64` and `osx-arm64` pins' `version`,
`protocols` and `features` equal what those builds print for `--version` in that run.

## Context

- BL-028 pinned stunnel/static-curl's 8.21.0 builds (ADR-0016) and added the download,
  verify and conformance steps to `.github/workflows/ci.yml`. A dark factory lane cannot
  push and `factory/**` branches do not run CI, so BL-028 could not observe a run; this
  task is its acceptance criterion 3, split out.
- static-curl's own build logs have expired (HTTP 410), so BL-028 filled `version` from
  the release notes' component list and the target triplet embedded in each binary
  (`x86_64-linux-musl`, `aarch64-apple-darwin`), and `protocols`/`features` from the
  release notes. ADR-0016 says the executable wins. The workflow's `Verify the upstream
  curl build` step prints `curl --version` and raises a `::warning` naming each field
  that differs from its pin.
- Read runs with `gh run list --workflow CI` and `gh run view <id> --log` (read only):
  the dark factory's shift branch and its merge pull request both run CI.
- If the Linux or macOS leg is red, the cause decides the fix: a hash mismatch is
  ADR-0016 decision 6 (Blocked for Stewart only if the asset itself changed); a failing
  conformance test on OpenSSL where Schannel passes is a per-platform answer to pin.

## Acceptance criteria

- [ ] The URL of a `CI` run containing BL-028's commits, green on all three legs, is in
      this task's Log.
- [ ] In that run, the Linux and macOS `Conformance tests` steps show the
      `Surl.Conformance.UnitTests` tests passed with `Skipped: 0`.
- [ ] That run's `Verify the upstream curl build` steps raise no `::warning` about a pin
      field, or the pins are corrected to the printed values and any difference is
      recorded under Notes.

## Notes

- 2026-09-28, run 1: no `CI` run contained BL-028's commits (`factory/**` pushes do not
  run CI and no merge pull request was open), so this run dispatched one on the shift
  branch: https://github.com/StewartScottRogers/Surl/actions/runs/36531859090 (head
  12dabd8). Windows green; Linux and macOS red in `Fast tests`, before any upstream curl
  step ran, so the pins and the conformance steps are still unobserved.
- Cause: `ServerTlsSettingsTests.CreateAuthenticationOptions_Intermediates_AreInTheCertificateContext`
  passed a self-signed authority as the "intermediate". On Linux and macOS
  `SslStreamCertificateContext` trims a chain's self-signed root, so the context held no
  intermediate (`Sequence contains no elements`). Fixed in the test: a real root ->
  intermediate -> leaf chain, handing over only the intermediate (partial chain, so no
  platform trims it). Added `Surl.Networking.UnitTests` to `touches` for this; no task in
  Doing names it (BL-032: Surl.Core, BL-062: Surl.Console).
- Also found: on Windows `SslStreamCertificateContext` adds intermediates to the user's
  `CurrentUser\CA` store. The old test had left 52 self-signed `CN=surl test intermediate`
  certificates there on this machine, and Windows' chain engine then picked one of them
  up ("An unknown chain building error occurred"). The intermediate's subject now carries a
  GUID per run. The store still gains one certificate per run on Windows; left as is (not
  deleted from Stewart's store by an unattended run).
- The fix is committed on its own (finished, build clean, fast tests green) so the shift
  pushes it; the acceptance criteria can only be checked on a `CI` run that contains it,
  which a lane cannot trigger before the shift pushes. Next run: `gh workflow run CI --ref
  <shift branch>` (or read the merge pull request's run), then check the three boxes.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Backlog. Waiting for the shift to push the networking test fix; the CI legs can only be checked on a run containing it (run 36531859090 was red on Linux/macOS in Fast tests)
- 2026-09-29: Backlog -> Doing.
