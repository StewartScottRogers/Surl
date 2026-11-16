---
id: BL-1305
title: Re-pin the Curl port at its current head for win-x64, linux-x64 and osx-arm64 in CurlPortBuilds.json
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1304]
touches: [CurlPortBuilds.json]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1305 — Re-pin the Curl port at its current head for win-x64, linux-x64 and osx-arm64 in CurlPortBuilds.json

## Goal

`CurlPortBuilds.json` pins the Curl port's `master` head on the day of the run, for `win-x64` (curl and Libcurl), `linux-x64` and `osx-arm64`.

## Context

ADR-0089 Amendment 8; Stewart chose "most recent" (2026-11-16). Resolve the head with `git ls-remote https://github.com/StewartScottRogers/Curl master`. win-x64 is built locally per the file's comment; linux-x64 and osx-arm64 hashes come from a `curl-port-phase-7` run (BL-1304) printing them. If BL-1304 has not produced a run, pin win-x64 and move this task to Backlog depending on BL-1304 for the rest.

## Acceptance criteria

- [ ] Every entry names one and the same full commit SHA, the head resolved that day.
- [ ] Each of `win-x64`, `linux-x64`, `osx-arm64` has a curl entry with `sha256`, `publishCommand` and `published`; `win-x64` also has its library entry.
- [ ] The CurlPortBuilds unit tests pass and the old `b42c319` entries are gone.

## Notes

- 2026-11-16: Head resolved as 74f3f45c47e86300b2b262dc1dacd43b508b09aa. win-x64 curl and Libcurl rebuilt and pinned (old b42c319 entries replaced). linux-x64 and osx-arm64 are not pinned: curl-port-phase-7.yml exists only on factory branches, not the default branch, so `gh` cannot find or dispatch it. Remaining work: run the workflow at this same commit, add the two entries, tick the boxes.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. win-x64 pinned at 74f3f45; linux-x64/osx-arm64 need a curl-port-phase-7 run (workflow not on master yet, BL-1304 produced no run)
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow is not on master so gh cannot dispatch it; retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master so gh cannot dispatch it (404); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (gh 404); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (not listed by gh); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (gh workflow list omits it); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (gh 404); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (gh workflow list omits it); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (gh 404); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (gh workflow list omits it); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (gh 404); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; could not confirm the workflow is on master (gh and git checks were denied this run); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master tree lacks it); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master workflows: ci, gource, release only); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master workflows: ci, gource, release only); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master workflows: ci, gource, release only); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master workflows: ci, gource, release only); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master workflows: ci, gource, release only); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master workflows: ci, gource, release only); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master workflows: ci, gource, release only); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master workflows: ci, gource, release only); retry once the shift merges it
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. linux-x64/osx-arm64 pins need a curl-port-phase-7 run; workflow still not on master (origin/master workflows: ci, gource, release only); retry once the shift merges it
