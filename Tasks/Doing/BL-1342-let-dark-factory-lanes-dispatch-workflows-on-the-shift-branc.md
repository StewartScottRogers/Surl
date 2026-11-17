---
id: BL-1342
title: Let dark factory lanes dispatch workflows on the shift branch and wait for the run instead of blocking for Stewart
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, .claude/skills/task-run/SKILL.md, .claude/skills/task-board/SKILL.md, Documentation/Wiki/Dark-Factory-Recipes.md]
requirement: none
created: 2026-11-17
completed:
---
# BL-1342 — Let dark factory lanes dispatch workflows on the shift branch and wait for the run instead of blocking for Stewart

## Goal

A lane whose task needs a CI result it cannot produce on Windows can start that result itself. It dispatches a workflow (e.g. `curl-port-phase-7.yml`, or `ci.yml` with `attempts=2`) on the shift branch after its commit is pushed, or reads the CI run its push started. It then waits for the run or parks the task with the run ID, and never blocks the task for Stewart over a dispatch.

## Context

- Stewart, 2026-11-17: "do it" / "yes", answering whether lanes should dispatch workflows themselves.
- Incidents, 2026-11-16 to 2026-11-17: BL-1305, BL-1317, BL-1318, BL-1307, BL-1339 and BL-1341 each blocked or requeued with "Stewart: dispatch curl-port-phase-7" or "a lane cannot push or dispatch workflows". The shift went idle until an interactive session dispatched the runs (44012635963, 44044472038), and BL-1305's requeue loop opened PRs #46 to #92.
- What is allowed:
  - Dispatching a workflow on `factory/phase-1` (the shift branch) is reversible and outward to this repository only, so it needs no confirmation.
  - It must never target `master`: `ci.yml`'s concurrency group would cancel master's own run (BL-1289's note).
  - Pushing remains the lane's integration step, as now.
- Design points:
  - A "waits for a run" state with the run ID in the task, so the coordinator re-offers the task when the run completes (`gh run view <id> --json status`). Consider a `Waiting` note or a Blocked reason with a machine-readable `run:<id>` that the coordinator polls and unblocks.
  - Rate-limit dispatches, so one task cannot start a run per claim.
  - `gh` auth is already available to lanes for PRs. Confirm that a lane process can call `gh workflow run` and `gh run view`.
  - Update the task-run skill's blocking guidance: dispatch and wait, don't block for Stewart, when the only missing thing is a CI run.

## Acceptance criteria

- [ ] A lane can dispatch a named workflow on the shift branch with inputs, and record the run ID on the task.
- [ ] The coordinator moves a task waiting on a run back to Backlog when the run completes, with the run's conclusion in the Log, and never re-offers it before then.
- [ ] No lane dispatches against `master`, and dispatches per task per shift are capped (a guard in the script, shown by a dry-run or test).
- [ ] The task-run skill, `task-board/SKILL.md` and `Dark-Factory-Recipes.md` describe the new path, and say that "dispatch a workflow" is never a reason to block for Stewart.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-17: Created; Stewart approved lanes dispatching workflows on the shift branch.
- 2026-11-17: Backlog -> Doing.
