---
id: BL-693
title: Confirm CI builds and verifies the re-pinned OpenLDAP upstream curl
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-690]
touches: [Build-OpenLdapUpstreamCurl.ps1, UpstreamCurlBuilds.json, .github/actions/upstream-curl]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-693 — Confirm CI builds and verifies the re-pinned OpenLDAP upstream curl

## Goal

A CI run on GitHub shows `Obtain the pinned upstream curl` passing on ubuntu-latest with BL-690's re-pin (ADR-0104).

## Context

- BL-690 (dark factory lane 4, 2026-11-05) changed `Build-OpenLdapUpstreamCurl.ps1` to install
  `musl=1.2.5-r13 musl-dev=1.2.5-r13` and re-pinned the supplementary linux-x64 entry in
  `UpstreamCurlBuilds.json` to `4418AD0292EB222B746096021817FE99E646E1506DA034A2607A264C21C39480`
  (ADR-0104). The build reproduced that SHA-256 three times locally on Docker Desktop.
- A lane cannot push, so BL-690 could not see a CI run on its own commit. This task checks the
  first `CI` workflow run on a commit that holds BL-690's change (the shift's branch or `master`):
  `gh run list --workflow CI`, then `gh run view <id> --log` for the ubuntu-latest leg.
- The first run misses the cache (its key holds the new SHA-256), so it builds in Docker; the
  step `Verify the OpenLDAP upstream curl build` then compares the SHA-256 against the pin.
- If the CI build hashes differently from the local one (runner Docker vs Docker Desktop), do
  not re-pin in CI (ADR-0016 decision 6): find why the bytes differ and record it in a new ADR.

## Acceptance criteria

- [x] A CI run on a commit holding BL-690's change passes `Obtain the pinned upstream curl` on ubuntu-latest; its URL is in Notes.
- [x] That run's `Verify the OpenLDAP upstream curl build` step passed, so the CI-built binary hashed to `4418AD02...C39480`; its log line is quoted in Notes.

## Notes

- CI runs on `push` only outside `factory/**`, so no run had covered BL-690's commit c475b336 yet. It was already on `origin/factory/phase-1` (head 1ff08ad5), so this task started the `CI` workflow there with `gh workflow run CI --ref factory/phase-1` (workflow_dispatch, which ci.yml allows). Decision: dispatching a CI run is reversible and changes nothing in the repository; waiting for the end-of-shift pull request would have left this task with nothing to check.
- Run: https://github.com/StewartScottRogers/Surl/actions/runs/41986202742 on 1ff08ad5. Every job passed: Build and test on windows, ubuntu and macos, and Upstream test cases on ubuntu and macos. On ubuntu-latest, `Obtain the pinned upstream curl` passed and so did `Conformance tests`.
- The cache missed as expected (`Cache not found for input keys: openldap-upstream-curl-linux-x64-4418AD02...C39480`), so the build ran in Docker on the runner. The verify step logged:
  `/opt/upstream-curl/8.21.0-openldap/curl matches the pin 4418AD0292EB222B746096021817FE99E646E1506DA034A2607A264C21C39480.`
  `curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0 OpenSSL/3.5.9 mit-krb5/1.22.2 OpenLDAP/2.6.15`
- The runner's Docker reproduced Docker Desktop's bytes, so no re-pin and no ADR was needed. The cache is now saved under the new key. Nothing in `touches` changed, so the build and tests did not need to run locally.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. CI run 41986202742 built the OpenLDAP upstream curl in Docker on ubuntu-latest and it matched the pin 4418AD02...C39480
