---
id: BL-1142
title: Record the three platforms' upstream test-case pass rates after the failing-case fixes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1126, BL-1127, BL-1128, BL-1129, BL-1130, BL-1131, BL-1132, BL-1133, BL-1134, BL-1136, BL-1137, BL-1139, BL-1140, BL-1141, BL-1143, BL-1144]
touches: [Documentation/Conformance]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1142 — Record the three platforms' upstream test-case pass rates after the failing-case fixes

## Goal

`Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` and the three
`upstream-test-cases-passing.<platform>.txt` lists record one CI run (Linux, macOS) and one local
Windows reference run of a commit that contains every task this depends on, and every case those
tasks named passes on the platforms they named.

## Context

- Baseline, 2026-11-14: CI run 43629075978 (head `356da7cd`): `linux-x64` 1,281 runnable, 1,259
  passed, 22 failed, 732 skipped; `osx-arm64` 1,281, 1,258, 23, 732. Local Windows reference run at
  `5a45202a`: 1,155 runnable, 1,154 passed, 1 failed (1452), 858 skipped.
- What should change, case by case:
  - Linux and macOS failures that should pass: 165, 469, 955-958, 962-968, 1448, 2046, 2047 (BL-1127),
    1800 (BL-1128), 1452 (BL-1129), 311, 312 (BL-1130); 1072, 1073 and macOS 4001 as BL-1141's tasks
    say.
  - Skips that should run and pass, on all three platforms unless named: 29, 160, 303, 1074
    (BL-1133); 190, 250, 251, 1086, 1112 (BL-1134); 126, 270, 1206, 1207, 1208 (BL-1136); 853
    (BL-1137); 1132, 1196, 2200, 2202, 2203, 2206 (BL-1139); 1194, 1195, 2207, 3017 (BL-1140); 48,
    338, 1134, 1419, 1421 everywhere and 206, 1008, 1418 on Linux and macOS (BL-1132); 2088, 2089 on
    Linux and macOS (BL-1131).
  - Windows: 1452 passes.
- How: follow BL-1121's steps. CI ignores pushes to `factory/**`, so dispatch it
  (`gh workflow run CI --ref factory/phase-1`), download `upstream-test-case-results-linux-x64` and
  `-osx-arm64` with `gh run download`, copy each passing list over the one in
  `Documentation/Conformance/`, and update each row, the skip table and a dated paragraph. Run the
  Windows suite locally (`dotnet test Conformance/Surl.Conformance.UnitTests --filter
  "FullyQualifiedName~UpstreamTestCasesAgainstSurlTests"`, about 75 s) for its row. A number leaves a
  list only with an exclusions row (ADR-0080 decision 5).
- A named case that still fails is not fixed here: file a task for it (lanes may file extra tasks)
  with its block from `upstream-test-case-failures.<platform>.txt` (BL-1126), and name it in Notes.
- This is a measurement with pinned upstream curl, not a code change.

## Acceptance criteria

- [ ] Notes hold the CI run URL and head commit, and the Windows run's commit; each contains every
      task in `depends-on`.
- [ ] The three rows and passing lists match those runs; `UpstreamTestCasePassRateStatementTests`
      passes.
- [ ] The skip table's columns match the runs; no `servercmd-unsupported` or
      `server-half-not-composed` skip is left for a case named above.
- [ ] Every case named in Context passes on the platforms named, or has a filed task named in Notes.

## Notes

## Log

- 2026-11-14: Created.
