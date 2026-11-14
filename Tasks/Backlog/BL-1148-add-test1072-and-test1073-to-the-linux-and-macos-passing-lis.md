---
id: BL-1148
title: Add test1072 and test1073 to the Linux and macOS passing lists after a CI run confirms BL-1143
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1143]
touches: [Documentation/Conformance/upstream-test-cases-passing.linux-x64.txt, Documentation/Conformance/upstream-test-cases-passing.osx-arm64.txt, Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1148 — Add test1072 and test1073 to the Linux and macOS passing lists after a CI run confirms BL-1143

## Goal

Tests 1072 and 1073 are on the `linux-x64` and `osx-arm64` passing lists and the pass-rate table counts them.

## Context

BL-1143 gives curl's stdin as a seekable file on Linux and macOS. Lanes cannot push or dispatch CI, so the CI confirmation was split out. Dispatch `gh workflow run CI --ref factory/phase-1` once BL-1143 is integrated, read the Upstream test cases job's newly-passing lines.

## Acceptance criteria

- [ ] A dispatched CI run shows `test1072` and `test1073` passing on `linux-x64` and `osx-arm64`.
- [ ] Their numbers are in both `upstream-test-cases-passing.<platform>.txt` lists and the pass-rate table matches.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Blocked. Needs a CI dispatch on factory/phase-1 after BL-1143 (ceff9446) is integrated; latest CI run (ca55e264) predates it and lanes cannot push or dispatch CI. Anyone with push rights can clear it, then re-run this task.
- 2026-11-14: Blocked -> Backlog. Unblocked: BL-1143 now Done
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Blocked. Stewart: no finished CI run contains BL-1143 (ceff9446); latest dispatch ca55e264 predates it and lanes cannot push or dispatch - dispatch CI on factory/phase-1 after the shift pushes, then re-run.
- 2026-11-14: Blocked -> Backlog. CI run 43651751537 (PR #36, 145f16b) ran the upstream cases with this fix: 956, 957, 958, 1072 and 1073 pass on linux-x64 and osx-arm64, and both failures files are empty
