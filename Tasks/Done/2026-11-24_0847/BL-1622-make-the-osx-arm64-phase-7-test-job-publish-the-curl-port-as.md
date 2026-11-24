---
id: BL-1622
title: Make the osx-arm64 Phase 7 test job publish the Curl port as the linux-x64 job does and accept its CI hash
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/curl-port-phase-7.yml]
requirement: FR-057
created: 2026-11-24
completed: 2026-11-24
---
# BL-1622 — Make the osx-arm64 Phase 7 test job publish the Curl port as the linux-x64 job does and accept its CI hash

## Goal

The `test-curl-port-osx-arm64` job in `.github/workflows/curl-port-phase-7.yml` installs the Curl port the way the `test-curl-port-linux-x64` job does, so its Phase 7 cases reach a comparison instead of throwing `UnpinnedCurlPortClientException`.

## Context

`CurlPortBuilds.json` pins the Curl port for `linux-x64` and `osx-arm64` at commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa` (published 2026-11-16), both with `ciHashStable: false`. On linux-x64, BL-1339 made the test job publish into `$RUNNER_TEMP` (the pin's `-o` removed) and copy the folder into the pinned path, and BL-1340 (ADR-0175 Amendment 3) made the job write the published file's hash to `SURL_CURL_PORT_CI_SHA256`, which `PinnedCurlPortClientOnThisMachine.CiAcceptedSha256` hands to `CurlPortClientLocator`. Since then linux-x64 compares (run 44044472038). The osx-arm64 job got neither change: it still runs the pin's `publishCommand` straight into the pinned folder and sets no `SURL_CURL_PORT_CI_SHA256`. In run 44044472038 its job log shows 357 of 489 tests failing with `UnpinnedCurlPortClientException` (published hash `51892DFE…C655`, pin `B5B48DC7…BBEE`), so nothing was compared on osx-arm64. In the scheduled run 45198867449 (2026-11-23, `master`) the osx-arm64 test job was cancelled after 15 minutes with no step run (no macOS runner picked it up); that is not this task's to fix, but note it if it recurs.

`UpstreamCurlBuilds.json` does pin an `osx-arm64` reference build (SHA-256 `04E0E69B…EEBF`, curl 8.21.0, pinned 2026-09-28), and the job already obtains it through `.github/actions/upstream-curl`. The family files' osx-arm64 sections that say otherwise are corrected by the task that records the next run, not here.

The workflow gates nothing (ADR-0089 Amendment 8, ADR-0003 decision 6). The port is never an oracle.

## Acceptance criteria

- [x] In `test-curl-port-osx-arm64`, the "Publish the Curl port to its pinned path" step removes the pin's `-o` argument, publishes into `$RUNNER_TEMP/curl-port-publish-one`, copies that folder's contents into `$HOME/.local/share/surl/curl-port/<commit>/` and runs `chmod +x` on its `curl`, as the linux-x64 job's step of the same name does.
- [x] The same step writes `SURL_CURL_PORT_CI_SHA256=<SHA-256 of the copied curl>` to `$GITHUB_ENV` and prints the hash.
- [x] The comment above the job names BL-1622 and says why (the BL-1339/BL-1340 treatment, now on osx-arm64).
- [x] No other job in the workflow changes, and `ci.yml` is untouched.

## Notes

No choices needed: copied the linux-x64 step verbatim (same pwsh, same $HOME path on macOS). Not verified by a CI run; the next Phase 7 run records it. Scheduled run 45198867449 cancelled the osx-arm64 job with no macOS runner; note it if it recurs.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. osx-arm64 job now publishes to a temp folder, copies to the pinned path and exports SURL_CURL_PORT_CI_SHA256
