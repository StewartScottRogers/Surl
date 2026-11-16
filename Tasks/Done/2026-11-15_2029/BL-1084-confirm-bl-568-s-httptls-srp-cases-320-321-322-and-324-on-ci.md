---
id: BL-1084
title: Confirm BL-568's httptls+srp cases 320, 321, 322 and 324 on CI's Linux and macOS legs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-568]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-12
completed: 2026-11-15
---
# BL-1084 — Confirm BL-568's httptls+srp cases 320, 321, 322 and 324 on CI's Linux and macOS legs

## Goal

A CI run containing BL-568's commits lists upstream test cases 320, 321, 322 and 324 by number in the
Linux and macOS conformance result lines, and the outcomes are recorded here.

## Context

BL-568 built the `httptls+srp` server half (`UpstreamTestCaseTlsSrpServerHalf`) and proved the Windows
skip (`feature-missing:TLS-SRP`); its CI criteria need a pushed commit a lane cannot make, so they moved
here, as BL-371's went to BL-724. Read runs with `gh run list --workflow CI` and `gh run view <id> --log`.

## Acceptance criteria

- [x] The URL of a `CI` run containing BL-568's commits is in Notes, with the result of cases 320, 321,
      322 and 324 on `ubuntu-latest` and `macos-latest`, each listed by number.
- [x] Failing cases are grouped by cause into new tasks filed with `task-planner`, their IDs in Notes;
      passing ones are added to the platform's passing list and pass-rate file.

## Notes

- 2026-11-15 (task-planner review): done by BL-1121. CI run
  https://github.com/StewartScottRogers/Surl/actions/runs/43629075978 (head 356da7cd, which contains
  BL-568's d8640ec7) produced the Linux and macOS passing lists that commit 1c2029966 (BL-1121) copied into
  `Documentation/Conformance/upstream-test-cases-passing.linux-x64.txt` and `.osx-arm64.txt`; cases 320,
  321, 322 and 324 pass on `ubuntu-latest` and `macos-latest` (lines 302 to 305 of each list). No case
  failed, so no task was filed. Later run 43788880386 (master, 2026-11-16) uploads the same artifacts
  `upstream-test-case-results-linux-x64` and `-osx-arm64`.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Deferred. No CI run contains BL-568's commit d8640ec7 yet (latest CI runs c5ad69c1 and 6eb8657d are not descendants); look again after the shift merges it to master and CI passes
- 2026-11-15: Reviewed by task-planner: finished. Done by BL-1121 (CI run 43629075978, commit 1c2029966): cases 320, 321, 322 and 324 pass on Linux and macOS; see Notes. Stays in Deferred only because task-board.ps1 allows Deferred -> Backlog alone; BL-1235 closes it to Done with BL-1234's close-split.
- 2026-11-15: Deferred -> Done. Closed by its parts, all Done: BL-1121
