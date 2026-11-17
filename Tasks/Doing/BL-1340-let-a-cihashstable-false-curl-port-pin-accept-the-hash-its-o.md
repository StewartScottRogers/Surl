---
id: BL-1340
title: Let a ciHashStable:false Curl port pin accept the hash its own CI run's build job reproduced (linux-x64 and osx-arm64 test jobs)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, CurlPortBuilds.json, .github/workflows/curl-port-phase-7.yml]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1340 — Let a ciHashStable:false Curl port pin accept the hash its own CI run's build job reproduced (linux-x64 and osx-arm64 test jobs)

## Goal

The linux-x64 and osx-arm64 test jobs of `curl-port-phase-7` run the Phase 7 families (and the 47 `OpenLdap_` cases) against the Curl port they publish, instead of throwing `UnpinnedCurlPortClientException`.

## Context

BL-1339 found that the test job's publish hashes to `048AAEE0...` (linux-x64) while its own build job reproduces the pin `DF507F32...`, in run 44012635963, 44021446938 and 44025033078, even after reordering steps and publishing the build job's way. The toolchain and runner image are identical, so the cause is not found. The pin check in `Conformance/Surl.Conformance.UnitLibrary/CurlPortClientLocator.cs` (`VerifyFirstPresent`) requires the file to hash to the pin. Decide how a pin with `ciHashStable: false` (ADR-0175 Amendment 2) is trusted in CI only: e.g. an environment variable naming an extra accepted hash that the workflow sets from the published file after its build job reproduced the pin twice, never accepting an upstream pin's hash. Add the field/setting, tests, and set `ciHashStable: false` for linux-x64 in `CurlPortBuilds.json`. Then redispatch `curl-port-phase-7` and record the outcome in each family file's linux-x64 and osx-arm64 sections (BL-1339, BL-1308 acceptance criteria).

## Acceptance criteria

- [ ] A unit test shows the locator accepts a hash the environment names for a `ciHashStable: false` pin and still refuses it for a pin without that field and for any hash in `UpstreamCurlBuilds.json`.
- [ ] A dispatched `curl-port-phase-7` run's linux-x64 test job runs the Phase 7 families with no `UnpinnedCurlPortClientException`.
- [ ] Each family file's linux-x64 section records the outcome.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
