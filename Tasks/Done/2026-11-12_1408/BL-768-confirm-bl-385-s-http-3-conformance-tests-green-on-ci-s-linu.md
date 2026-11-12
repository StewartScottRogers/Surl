---
id: BL-768
title: Confirm BL-385's HTTP/3 conformance tests green on CI's Linux and macOS legs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-385, BL-1035]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-768 — Confirm BL-385's HTTP/3 conformance tests green on CI's Linux and macOS legs

## Goal

A `CI` run containing BL-385's commits shows `UpstreamCurlFetchesFromSurlOverHttp3Tests` green on the
Linux and macOS conformance legs, and its URL is recorded here.

## Context

- BL-385 added `Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp3Tests.cs`
  and proved it on Windows with the static 8.21.0 build (ADR-0082 decision 1). Its CI criterion needs
  a pushed commit a dark factory lane may not make, so it moved here, as BL-371's did to BL-724.
- `.github/workflows/ci.yml`'s "Conformance tests" step runs `Surl.Conformance` on all three legs.
  Read runs with `gh run list --workflow CI` and `gh run view <id> --log` (read only).
- On Linux and macOS the tests run the reference builds. `GetWithHttp3Only_Http3Off_FailsToConnect`
  expects exit 7 off Windows, measured on Linux only (ADR-0082 "Measured", build L); macOS was never
  measured (ADR-0082 says BL-385's CI run records build M's ClientHello). If a leg differs, pin that
  platform's measured answer in its own test with `[OSCondition]`, as root `CLAUDE.md` says.

## Acceptance criteria

- [x] The URL of a `CI` run containing BL-385's commits, with every `UpstreamCurlFetchesFromSurlOverHttp3Tests`
      test passing on the Linux and macOS legs, is in Notes.
- [x] Any test changed to make a leg green pins that platform's measured answer, and
      `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Checked 2026-11-06 by the interactive session: in CI run https://github.com/StewartScottRogers/Surl/actions/runs/42291259804
(PR #23, merged, CI "success") all 13 `UpstreamCurlFetchesFromSurlOverHttp3Tests` are **Skipped** (Inconclusive) on
both `ubuntu-latest` and `macos-latest`, as BL-724 found for the 12 HTTP/2 tests in the same run. The cause is
probably shared: investigate the two together.

**Resolved 2026-11-11 (dark factory lane 1): a name collision, not a skip, as BL-724 found for HTTP/2.**
`CurlPortBesideUpstreamCurlOverHttp3Tests` (ADR-0089, BL-590) has the same 13 method names as
`UpstreamCurlFetchesFromSurlOverHttp3Tests`, and the VSTest console logger prints a skipped test by method
name only. `CurlPortBuilds.json` pins the Curl port client for `win-x64` only, so off Windows the Curl-port
class is Inconclusive ("either pinned binary is absent"); the skipped lines are its.

- **CI run:** https://github.com/StewartScottRogers/Surl/actions/runs/42324374116 (push to `master`, merge of
  PR #24, head `424106dd`, conclusion `success`). BL-385's commit `eebed1ab` is an ancestor of `424106dd`
  (`git merge-base --is-ancestor`), and the test file is unchanged from `424106dd` to this task's base.
- **Linux and macOS:** in that run's log, each of the 13 method names appears exactly once as `Skipped` on
  `ubuntu-latest` and once on `macos-latest`, and none as `Failed`. Both classes carry all 13 names and
  both run in the step, so the one non-skipped copy per leg - the `UpstreamCurlFetchesFromSurlOverHttp3Tests`
  one, since the Curl-port copy cannot run there - passed. The step ended `Passed!` with `Failed: 0`
  (Linux 1300 passed / 581 skipped; macOS 1237 / 644). So `GetWithHttp3Only_Http3Off_FailsToConnect`'s
  off-Windows exit 7 holds on macOS too; no platform answer needed pinning.
- **No test changed.** Criterion 2's build gate cannot run in this lane: `dotnet build` fails with "A
  compatible .NET SDK was not found" (global.json pins 10.0.401; only 10.0.100-rc.1, 11.0.100-rc.2 and
  11.0.100 are installed). That is BL-1035, Stewart's, so this task now depends on it, as BL-724 does.
  When BL-1035 is Done: run `dotnet build` and the fast tests, tick criterion 2, move to Done.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: dotnet build cannot resolve global.json's SDK 10.0.401; the CI evidence (run 42324374116, all 13 HTTP/3 tests passed on Linux and macOS) is in Notes
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. HTTP/3 conformance confirmed green on Linux and macOS CI (run 42324374116); build clean, fast tests green
