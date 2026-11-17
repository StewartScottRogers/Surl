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
completed: 2026-11-16
---
# BL-1314 — Build the libtest programs in the Debug upstream curl build script (ADR-0174)

## Goal

The Debug build script also builds tests/libtest against its static Debug libcurl, reproducibly.

## Context

ADR-0174 decision 1; extends the script of BL-1310 (ADR-0173). Same container, digest and packages; build twice and compare.

## Acceptance criteria

- [x] Build-DebugUpstreamCurl.ps1 runs make -C tests/libtest and copies every program to /opt/upstream-curl/8.21.0-debug/libtest.
- [x] Two builds produce identical hashes for every program, and the script lists programs that fail to link by name.

## Notes

- The script now runs `make -k -C tests/libtest` against the static Debug libcurl, strips and copies every ELF program to a `libtest` directory beside OutputPath (install it as /opt/upstream-curl/8.21.0-debug/libtest), prints each program's SHA-256, warns by name for any that fail to link (parsed from make -k's errors), and with -CompareTwoBuilds throws on any differing program hash.
- Measured 2026-11-16: curl 8.21.0 builds ONE multi-call program, `libtests` (SHA-256 040083EC...EF4D, identical in two clean builds; curl 0B847174...04AA unchanged). No link failures. BL-1313's `programs` map will therefore hold one entry; the runner must invoke it per the 8.21.0 multi-call convention (tool name as first argument) - for the runner task.
- Fix found on the way: nested double quotes inside `$(...)` in the build script are mangled by PowerShell's native argument passing; avoided with a pipeline.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Debug build script builds tests/libtest (one multi-call libtests program), reproducibly, naming link failures
