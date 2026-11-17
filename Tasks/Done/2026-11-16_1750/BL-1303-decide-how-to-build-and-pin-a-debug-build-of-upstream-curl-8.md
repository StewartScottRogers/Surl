---
id: BL-1303
title: Decide how to build and pin a Debug build of upstream curl 8.21.0 for the http/2 and http/3 upstream test cases that need one, and file the tasks that do it
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1303 — Decide how to build and pin a Debug build of upstream curl 8.21.0 for the http/2 and http/3 upstream test cases that need one, and file the tasks that do it

## Goal

An ADR decides how a Debug (`--enable-debug`) build of upstream curl 8.21.0 is produced and pinned in `UpstreamCurlBuilds.json`, and the tasks that build, pin and run it against the http/2 and http/3 upstream test cases that need it are on the board.

## Context

- Ten of the 16 http/2 and http/3 upstream test cases are not run today because they need a Debug build of upstream curl or are libtests (found by the roadmap planning of 2026-11-16, BL-1288 to BL-1302). `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` lists the cases not run and why.
- **Approved.** Stewart, 2026-11-16, answering "pinning a Debug build is a download of upstream curl, do you want one?": **"download"**. The download or build of upstream curl 8.21.0 sources (https://github.com/curl/curl, tag `curl-8_21_0`) for this purpose needs no further approval. Any other upstream version, or any further download beyond this build, still needs his approval.
- upstream curl publishes no Debug binaries, so the decision covers building from the pinned 8.21.0 source: on which platforms (CI's Linux leg at least; Windows if lanes must run it), with which TLS, nghttp2 and HTTP/3 (ngtcp2/nghttp3 or quiche) backends the cases need, whether libtests are built too, how the result's SHA-256 is fixed and checked (built once and fetched by hash as `.github/actions/upstream-curl` does, or a reproducible build), and how it sits beside the reference build in `UpstreamCurlBuilds.json` without breaking "exactly one reference build per platform".
- Standing rules: upstream curl is the only oracle (ADR-0003); the Curl port is never used; `Record-CurlExchange.ps1` refuses unpinned binaries; base class library only, no new package.

## Acceptance criteria

- [x] An ADR marked "Decided by Claude under Stewart's delegation", citing Stewart's 2026-11-16 approval, decides how the Debug build of upstream curl 8.21.0 is built, on which platforms, with which backends, and how it is pinned and checked, with the alternatives considered.
- [x] The ADR names which of the ten http/2 and http/3 cases the build will run and which remain unrunnable and why.
- [x] The follow-up tasks are filed with `task-board.ps1 new`, each sized for one run with exact `touches` and checkable criteria: at least building and pinning the build, running the named cases with it, and updating `Upstream-Test-Case-Pass-Rate.md`. Their IDs are in Notes.

## Notes

- Decided in ADR-0173 (Decided by Claude under Stewart's delegation). Six of the ten cases (2400, 2401, 2406, 2501, 358, 359) need `Debug`; four (2402, 2404, 2407, 2502) are libtests and stay unrunnable by this build.
- Follow-up tasks: BL-1310 (build and pin), BL-1311 (run the six cases), BL-1312 (update Upstream-Test-Case-Pass-Rate.md), BL-1313 (libtest decision).
- Edited only Documentation/Planning/Decisions and the board, inside `touches`.

## Log

- 2026-11-16: Created. Stewart approved the Debug upstream curl 8.21.0 build ("download").
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ADR-0173 decides the Debug build; BL-1310 to BL-1313 filed
