---
id: BL-724
title: Confirm BL-371's HTTP/2 conformance tests green on CI's Linux and macOS legs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-371, BL-1035]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-724 — Confirm BL-371's HTTP/2 conformance tests green on CI's Linux and macOS legs

## Goal

A `CI` run containing BL-371's commits shows `UpstreamCurlFetchesFromSurlOverHttp2Tests` green on the
Linux and macOS conformance legs, and its URL is recorded here.

## Context

- BL-371 added `Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp2Tests.cs`
  and proved it on Windows with the static 8.21.0 build (ADR-0081 decision 1). Its criterion 3 (CI's
  Linux and macOS legs) needs a pushed commit a dark factory lane may not make, so it moved here, as
  BL-028's did to BL-079.
- `.github/workflows/ci.yml`'s "Conformance tests" step runs `Surl.Conformance` on all three legs.
  Read runs with `gh run list --workflow CI` and `gh run view <id> --log` (read only); the shift
  branch's merge pull request runs CI with these commits.
- On Linux and macOS the tests run the reference builds. If a leg differs from Windows (for example
  curl's `-v` wording for connection reuse), pin that platform's answer in its own test with
  `[OSCondition]`, as root `CLAUDE.md` says.

## Acceptance criteria

- [x] The URL of a `CI` run containing BL-371's commits, with every `UpstreamCurlFetchesFromSurlOverHttp2Tests`
      test passing on the Linux and macOS legs, is in Notes.
- [x] Any test changed to make a leg green pins that platform's measured answer, and
      `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Checked 2026-11-06 by the interactive session: CI run https://github.com/StewartScottRogers/Surl/actions/runs/42291259804
(PR #23, merged, CI "success") contains BL-371's commits, but all 12 `UpstreamCurlFetchesFromSurlOverHttp2Tests`
are **Skipped** (Inconclusive) on both `ubuntu-latest` and `macos-latest` - not passed; the run is green only
because a skip is not a failure. On Linux and macOS the test calls `PinnedUpstreamCurl.RunAsync` (the reference
build), and the same legs pass 1300 / 1237 other conformance tests, so the reference build is installed;
the skip reason is not in the log. Find why these 12 go Inconclusive there (run with `--logger "console;verbosity=detailed"`
on a Linux leg, or read `PinnedUpstreamCurl`'s Inconclusive paths) and make them run. The same "Conformance tests"
step skips 581 of 1881 tests on Linux and 644 on macOS, and `ci.yml` keeps no results file for it (only the
upstream test-case job uploads one), so the skip reasons are lost: adding `--logger trx` and an upload of the
results to that step is the quickest way to read them.

**Resolved 2026-11-11 (dark factory lane 3): the 2026-11-06 reading was a name collision, not a skip.**
`CurlPortBesideUpstreamCurlOverHttp2Tests` has the same 15 method names as
`UpstreamCurlFetchesFromSurlOverHttp2Tests`, and the VSTest console logger prints a skipped test by method
name only, with no class and no reason. The skipped names in the log are the Curl-port class's: it needs a
pinned Curl port client and a Windows supplementary build, so off Windows it is Inconclusive.

- **CI run:** https://github.com/StewartScottRogers/Surl/actions/runs/42324374116 (push to `master`, merge of
  PR #24, head `424106dd`, conclusion `success`). It contains BL-371's commits (`42d9d082`, `27dcfa8f` are
  ancestors of `424106dd`), and the test file is unchanged from `424106dd` to this task's base.
- **Linux and macOS:** in that run's "Conformance tests" step, each of the 15 method names appears exactly
  once as `Skipped` on `ubuntu-latest` and once on `macos-latest`, and none appears as `Failed`. Both classes
  carry all 15 names, so one copy per leg was not skipped. The step ended `Passed!` with `Failed: 0`
  (Linux 1300 passed / 581 skipped; macOS 1237 / 644), so the `UpstreamCurlFetchesFromSurlOverHttp2Tests`
  copies passed on both legs.
- **Which copy skips, measured:** this checkout's `Surl.Conformance.UnitTests` was published self-contained for
  linux-x64 as a Microsoft.Testing.Platform executable, from a temporary `git archive` copy outside the
  repository (the copy only, built with SDK 11.0.100 because of BL-1035). It was run in WSL Ubuntu against
  the pinned `/opt/upstream-curl/8.21.0/curl` (SHA-256 `153CA463…4E45`, verified). Alone, the class gives
  15/15 passed. In the full assembly with `--output detailed`, each name has one `passed` line and one
  `skipped` line. The skip reason is "UpstreamCurlBuilds.json pins no supplementary upstream curl build for
  linux-x64", which comes from the Curl-port class's Windows-only supplementary-build path.
- **No test changed**, so no platform answer needed pinning. `dotnet build` cannot run in this lane
  (global.json's SDK 10.0.401 is not installed; BL-1035, Stewart's). This task changes only this task file,
  so no build or test gate applies to it beyond that. The same tree's build and fast tests are green in the CI
  run above.
- Seen in passing, not this task's scope: the WSL run also failed `ParallelSurlProcessesTests` (no `dotnet` on
  WSL's PATH) and `GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback` (it fell back to HTTP/2 under WSL).
  CI does not show either failure, so both are local-environment artifacts. To keep skip reasons, `ci.yml`
  could still add `--logger trx` to the Conformance step; that is not filed, because the reasons can now be
  read as above.

**2026-11-11 (dark factory lane 2):** lane 3's findings above were carried over from branch
`factory/BL-724-lane-3-20261111-060243` (its only change was this task file). Its integration failed
because `dotnet build` cannot resolve global.json's SDK 10.0.401 on this machine, not because of the
work. Criterion 2 stays unticked until a build and the fast tests can run, so this task now depends on
BL-1035. When BL-1035 is Done: run `dotnet build` and the fast tests, tick criterion 2, move to Done.

**2026-11-12 (dark factory lane 2):** SDK 10.0.404 now resolves (BL-1035 done). No test changed;
`dotnet build` clean (0 warnings, 0 errors) and the fast tests pass with 0 failures. Criterion 2 ticked.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Backlog. Not confirmable yet: on CI run 42291259804 all 12 HTTP/2 conformance tests are skipped (Inconclusive) on Linux and macOS, not passed; findings in Notes for a lane to investigate
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 3 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-724-lane-3-20261111-060243; start with git cherry-pick --no-commit factory/BL-724-lane-3-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: dotnet build cannot resolve global.json's SDK 10.0.401; the CI evidence is recorded in Notes
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. HTTP/2 conformance tests confirmed passing on CI Linux and macOS (run 42324374116); build clean, fast tests green
