---
id: BL-1038
title: Print the build option probe test's per-probe lines in CI's Conformance tests step
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1035]
touches: [.github/workflows/ci.yml]
requirement: none
created: 2026-11-11
completed:
---
# BL-1038 — Print the build option probe test's per-probe lines in CI's Conformance tests step

## Goal

A green CI run's `Conformance tests` step log on `ubuntu-latest` and `macos-latest` shows the
`TestContext` lines of `UpstreamCurlShowsItsBuildOptionsTests.Probe_EveryOptionTheReferencePinClaims_ShowsTheOptionBuiltIn`
(each probe's `upstream curl <sha> exited <n>` line and each `not probed` line), not only failures.

## Context

- BL-945 found that run 42324374116 (master `424106dd`) passed the probe test on both legs, but
  `dotnet test` at normal console verbosity prints a passing test's `TestContext` output nowhere,
  so the per-probe commands and exit codes BL-945 meant to record were not in the log.
- `.github/workflows/ci.yml`, job `test`, step `Conformance tests`. Options: add
  `--logger "console;verbosity=detailed"` (prints every test's messages - check the log stays
  readable), or run the probe test in its own step with detailed verbosity, or write a `trx`
  and upload it as an artifact as the `upstream-test-cases` job does with its results.

## Acceptance criteria

- [ ] `ci.yml`'s Linux and macOS legs print or publish the probe test's `TestContext` lines on a passing run.
- [ ] The change is described in the step's comment in `ci.yml`.

## Notes

- 2026-11-11 (lane 3): Chose a separate `Build option probe test` step that runs only
  `UpstreamCurlShowsItsBuildOptionsTests` with `--logger "console;verbosity=detailed"`, and excluded
  that class from the `Conformance tests` step, so only the probe test's output is detailed and the
  rest of the log stays readable (the project runs on VSTest, so `--logger` applies). The `ci.yml`
  edit was made but not committed: `dotnet build` fails on this machine because the pinned SDK
  10.0.401 is not installed (only 10.0.100-rc.1 and 11.0.x), so the verify gate cannot pass.
  Returned to Backlog behind BL-1035; redo the one-step edit when it is Done.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot verify the ci.yml change
- 2026-11-12: Backlog -> Doing.
