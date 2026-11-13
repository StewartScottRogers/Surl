---
id: BL-1104
title: Let a manual CI run repeat the Linux and macOS test legs to expose flaky tests
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/ci.yml, Documentation/Wiki/Dark-Factory-Recipes.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-1104 — Let a manual CI run repeat the Linux and macOS test legs to expose flaky tests

## Goal

A manual run of the `CI` workflow can run the "Build and test" leg on Linux and macOS several
times side by side on one commit, so one dispatch shows every test that fails some of the
time on those platforms. Push and pull request runs behave exactly as today.

## Context

PRs #27 to #31 (frozen `factory/merge-*` branches) each failed CI on a different
timing-sensitive test on Linux or macOS, and each one passed on Windows, the only platform lanes
test on. Fixing one per CI run keeps the dark factory's merge to `master` held, so the next
step (BL-1105) needs one run that shows them all.

- `.github/workflows/ci.yml` already has `workflow_dispatch:`. Its `test` job's matrix is
  `include` rows for windows-latest, ubuntu-latest and macos-latest. It runs "Fast tests" then
  "Conformance tests" and has `timeout-minutes: 30`.
- `concurrency: ci-${{ github.ref }}` with `cancel-in-progress: true` means a second
  dispatch on the same ref cancels the first. So the repeats must be legs inside one run (for
  example a matrix `attempt` dimension built from a dispatch input), not separate dispatches.
- One approach: a `workflow_dispatch` input, e.g. `attempts` (number, default 1). When it is
  above 1, the Linux and macOS legs of `test` run that many times in parallel, each named
  so its attempt shows, e.g. `Build and test (ubuntu-latest, attempt 3)`. Windows and the
  `upstream-cases` job run once as today.
- Each repeated leg must leave a list of the tests it failed where BL-1105 can read it without
  scraping logs by hand, e.g. a `trx` logger result uploaded as an artifact named by OS and
  attempt.

## Acceptance criteria

- [ ] `ci.yml` declares a `workflow_dispatch` input that sets the number of Linux and macOS "Build and test" attempts, and push and pull request runs still run each leg once with today's job names.
- [ ] With the input above 1, every attempt runs both "Fast tests" and "Conformance tests", keeps running after a failed test step so each attempt reports in full, and uploads its test results (`.trx`) as an artifact named by OS and attempt.
- [ ] `Documentation/Wiki/Dark-Factory-Recipes.md` has a short recipe: the exact `gh workflow run CI --ref <branch> -f <input>=<n>` command, and how to list the failed tests from the run's artifacts with `gh run download`.
- [ ] The Log quotes the new `on.workflow_dispatch.inputs`, `strategy.matrix` and any changed `if:` expressions from the diff, and explains why a push or pull request run still gets one attempt per OS. Lanes cannot run the workflow before the shift pushes it, and BL-1105's run is its first live proof.

## Notes

Filed by an interactive session on 2026-11-13 at Stewart's request ("file the flake hunt").
Lanes cannot push or dispatch on another branch, so this task only changes the workflow;
BL-1105 runs it.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing.
