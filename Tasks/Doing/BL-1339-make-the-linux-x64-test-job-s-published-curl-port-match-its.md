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

- 2026-11-16: The reordered workflow (f2d931f68) was unpushed, and the latest `curl-port-phase-7` run (44012635963) used the old order. A lane may not push or dispatch on unpushed code, so the rerun waits for the shift to push the fix. Then: `gh workflow run curl-port-phase-7.yml --ref <branch>`, read the linux-x64 test job's printed hash, record the outcome in each family file's linux-x64 section.

- 2026-11-16 (second attempt): The reorder was pushed and run 44021446938 dispatched on factory/phase-1. It did NOT fix it: the linux-x64 test job again published `048AAEE0...` (441 failed, `UnpinnedCurlPortClientException`) while the build job of the same run reproduced `DF507F32...` twice. Same runner image (ubuntu-26.04 20261111.196.1) and same SDK pin in both Curl's and Surl's `global.json`, so the toolchain is not the cause. The remaining difference is the output folder: the build job publishes with the pin's `-o` removed into `$RUNNER_TEMP/curl-port-publish-*`, the test job published straight into `~/.local/share/surl/curl-port/<commit>`. Change made: the test job now publishes the build job's way and copies the folder to the pinned path (`chmod +x`), printing the hash. osx-arm64's test job (BL-1308) fails the same way with `80965EA6...` but is out of this task's scope. Next: the shift pushes this commit, then `gh workflow run curl-port-phase-7.yml --ref factory/phase-1`, read the printed hash. If it still differs, the fallback is a test-side rule (a test job may trust the hash its own build job reproduced), which is a conformance-library change and needs its own task.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Needs the reordered workflow commit f2d931f68 pushed by the shift before curl-port-phase-7 can be dispatched; a lane may not push
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Reorder did not fix it (run 44021446938 still hashed 048AAEE0); test job now publishes like the build job, needs the shift to push then redispatch curl-port-phase-7
- 2026-11-16: Backlog -> Doing.
