---
id: BL-079
title: Confirm the Linux and macOS conformance legs green in CI and correct the pins from the builds' own --version
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-028]
touches: [UpstreamCurlBuilds.json, .github/workflows/ci.yml]
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

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
