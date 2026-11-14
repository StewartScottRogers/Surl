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
completed: 2026-11-13
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

- [x] `ci.yml` declares a `workflow_dispatch` input that sets the number of Linux and macOS "Build and test" attempts, and push and pull request runs still run each leg once with today's job names.
- [x] With the input above 1, every attempt runs both "Fast tests" and "Conformance tests", keeps running after a failed test step so each attempt reports in full, and uploads its test results (`.trx`) as an artifact named by OS and attempt.
- [x] `Documentation/Wiki/Dark-Factory-Recipes.md` has a short recipe: the exact `gh workflow run CI --ref <branch> -f <input>=<n>` command, and how to list the failed tests from the run's artifacts with `gh run download`.
- [x] The Log quotes the new `on.workflow_dispatch.inputs`, `strategy.matrix` and any changed `if:` expressions from the diff, and explains why a push or pull request run still gets one attempt per OS. Lanes cannot run the workflow before the shift pushes it, and BL-1105's run is its first live proof.

## Notes

Design: a new `plan-test-matrix` job builds the `test` job's matrix as JSON (a matrix cannot
be sized from an input otherwise). Rows get an `attempt` field only on a dispatch with
`attempts` above 1. `--logger trx` is always on the test steps (harmless); the upload runs only
when `matrix.attempt` is set. Not run live: no .cs changed, so no build/test needed; the
planner script's JSON was checked locally with pwsh.

Filed by an interactive session on 2026-11-13 at Stewart's request ("file the flake hunt").
Lanes cannot push or dispatch on another branch, so this task only changes the workflow;
BL-1105 runs it.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing.
- 2026-11-13: Workflow change. `on.workflow_dispatch.inputs`: `attempts: {type: number, default: 1}`. `test.strategy.matrix: ${{ fromJSON(needs.plan-test-matrix.outputs.matrix) }}` with `needs: plan-test-matrix`; job name `Build and test (${{ matrix.os }}${{ matrix.attempt && format(', attempt {0}', matrix.attempt) || '' }})`. Changed `if:` on the Obtain, Conformance and probe steps: `matrix.upstream-curl-platform && (success() || (matrix.attempt && !cancelled()))`; upload step `if: matrix.attempt && !cancelled()`, artifact `test-results-${{ matrix.os }}-attempt-${{ matrix.attempt }}`. Push and pull request runs: `plan-test-matrix` reads ATTEMPTS as 1 when the event is not workflow_dispatch, so it emits one row per OS with no `attempt` field; the name suffix is then empty (same job names as before), the `if:` falls back to `success()` (as before) and no upload runs. First live proof is BL-1105's run.
- 2026-11-13: Doing -> Done. CI dispatch input attempts repeats Linux and macOS test legs with per-attempt trx artifacts; recipe in Dark-Factory-Recipes
