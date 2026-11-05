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
completed:
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

- [ ] A CI run on a commit holding BL-690's change passes `Obtain the pinned upstream curl` on ubuntu-latest; its URL is in Notes.
- [ ] That run's `Verify the OpenLDAP upstream curl build` step passed, so the CI-built binary hashed to `4418AD02...C39480`; its log line is quoted in Notes.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
