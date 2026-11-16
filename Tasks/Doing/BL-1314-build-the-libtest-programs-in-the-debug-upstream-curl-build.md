---
id: BL-1314
title: Build the libtest programs in the Debug upstream curl build script (ADR-0174)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1310]
touches: [Build-DebugUpstreamCurl.ps1]
requirement: none
created: 2026-11-16
completed:
---
# BL-1314 — Build the libtest programs in the Debug upstream curl build script (ADR-0174)

## Goal

The Debug build script also builds tests/libtest against its static Debug libcurl, reproducibly.

## Context

ADR-0174 decision 1; extends the script of BL-1310 (ADR-0173). Same container, digest and packages; build twice and compare.

## Acceptance criteria

- [ ] Build-DebugUpstreamCurl.ps1 runs make -C tests/libtest and copies every program to /opt/upstream-curl/8.21.0-debug/libtest.
- [ ] Two builds produce identical hashes for every program, and the script lists programs that fail to link by name.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
