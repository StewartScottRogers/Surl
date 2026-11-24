---
id: BL-1624
title: Dispatch curl-port-phase-7 and record its linux-x64 and osx-arm64 outcome in each Phase 7 family file
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-1622, BL-1623]
touches: [Documentation/Conformance/CurlPort]
requirement: FR-057
created: 2026-11-24
completed:
---
# BL-1624 — Dispatch curl-port-phase-7 and record its linux-x64 and osx-arm64 outcome in each Phase 7 family file

## Goal

A `curl-port-phase-7` run made after BL-1622 and BL-1623 is recorded, with per-family counts, in the `linux-x64` and `osx-arm64` sections of every family file in `Documentation/Conformance/CurlPort/`, and the false osx-arm64 statements there are gone.

## Context

Run it with `.claude/skills/task-board/dispatch-workflow.ps1 -Id BL-1624 -Workflow curl-port-phase-7 -Ref <shift branch>` and park this task with `move -To Blocked -Reason "Waiting for run:<id> (curl-port-phase-7)"` (task-board skill, "Waiting on a workflow run"); the coordinator returns it to Backlog when the run completes. Read the `phase-7-linux-x64-results` and `phase-7-osx-arm64-results` artifacts (`gh run download <id>`), or the job logs' `Total tests` / `Passed` / `Failed` / `Skipped` lines and each `Failed <test>` entry.

What the family files say now, and why it must change:
- Every family file's `## osx-arm64` section (from BL-1324, run 44000208980) says "`UpstreamCurlBuilds.json` pins no upstream curl build for `osx-arm64`". That is false: `UpstreamCurlBuilds.json` pins an `osx-arm64` reference build (SHA-256 `04E0E69B…EEBF`, curl 8.21.0, pinned 2026-09-28). The real cause was the port hash: in run 44044472038 the osx-arm64 job logged 357 `UnpinnedCurlPortClientException` failures, which BL-1622 removes.
- Line 3 of each family file names run 44044472038 (2026-11-17) as the current linux-x64 outcome. The later scheduled run 45198867449 (2026-11-23, `master` at `986449355167a9c352b9d4a0fdb6bda17e9d0239`) had 400 passed, 21 failed, 68 skipped on linux-x64; this task's run supersedes both.
- The osx-arm64 reference build lists no `ldap` and no `rtsp` protocol, so the LDAP and RTSP families are expected to be Inconclusive on osx-arm64; say so with the locator's message rather than as a finding.

Record only; classifying failures is BL-1625 (osx-arm64) and BL-1626 (linux-x64). The Curl port is never an oracle (ADR-0003); the workflow gates nothing (ADR-0089 Amendment 8).

## Acceptance criteria

- [ ] A `curl-port-phase-7` run dispatched by this task, on a commit that contains BL-1622 and BL-1623, completed with both test jobs run (not skipped or cancelled); its run ID and Surl commit are in this task's Notes.
- [ ] Each family file's `## osx-arm64` section names that run, the Surl commit, Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, and the family's passed / failed / not-executed counts, and no longer says that no upstream curl build is pinned for osx-arm64.
- [ ] Each family file's `## linux-x64` section, and its line-3 summary, name that run and the family's counts in the same form.
- [ ] `CurlPortBesideUpstreamCurlOverHttp2Tests` shows executed (not "not executed") cases on both platforms, as recorded in `Http2.md`; if it does not, the reason is written in `Http2.md` and a follow-up task is filed.
- [ ] The LDAP and RTSP families' osx-arm64 sections state the Inconclusive reason the run logged.

## Notes

## Log

- 2026-11-24: Created.
