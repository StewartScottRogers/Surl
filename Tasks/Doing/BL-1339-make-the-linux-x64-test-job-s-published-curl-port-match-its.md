---
id: BL-1339
title: Make the linux-x64 test job's published Curl port match its pin and rerun curl-port-phase-7
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [.github/workflows/curl-port-phase-7.yml, CurlPortBuilds.json, Documentation/Conformance/CurlPort]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1339 — Make the linux-x64 test job's published Curl port match its pin and rerun curl-port-phase-7

## Goal

The linux-x64 test job of `curl-port-phase-7` publishes a Curl port client whose SHA-256 matches `CurlPortBuilds.json`, so the Phase 7 families and the 47 `OpenLdap_` cases compare against upstream curl instead of throwing `UnpinnedCurlPortClientException`.

## Context

BL-1307 found that run 44012635963 published `048AAEE0...` in the test job against the pin `DF507F32...` that the build job of the same run reproduced. BL-1307 moved the upstream-curl/OpenLDAP step after the port publish (the suspect) and prints the published hash. Needs the commit pushed, `curl-port-phase-7` dispatched, and its result read; if the hash still differs, set `ciHashStable: false` handling for linux-x64 as osx-arm64 does (ADR-0175 Amendment 2) or re-pin.

## Acceptance criteria

- [ ] A run of `curl-port-phase-7` on linux-x64 passes cases (not `UnpinnedCurlPortClientException`) for the Phase 7 families.
- [ ] The 47 `OpenLdap_` cases compare on linux-x64 and each family file's linux-x64 section records the outcome.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
