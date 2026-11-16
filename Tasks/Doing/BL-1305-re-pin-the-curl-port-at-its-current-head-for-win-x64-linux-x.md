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

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
