---
id: BL-1319
title: Stop RunDarkFactory.ps1 opening a merge PR or starting a new shift when a shift changed only task bookkeeping
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, Documentation/Wiki/Dark-Factory-Recipes.md]
requirement: none
created: 2026-11-16
completed:
---
# BL-1319 — Stop RunDarkFactory.ps1 opening a merge PR or starting a new shift when a shift changed only task bookkeeping

## Goal

A shift whose only commits since the last frozen merge PR are task-board bookkeeping (claim, requeue, block, unblock) neither opens a new `factory/merge-*` PR nor, under `-Continuous`, starts another shift at once, and a task a lane requeues without progress is not claimed again in a tight loop.

## Context

- Incident, 2026-11-16 12:15-13:32 (UTC-7): BL-1305 needed `.github/workflows/curl-port-phase-7.yml` on `master` (GitHub dispatches only default-branch workflows). A lane claimed it, found the workflow missing and requeued it to Backlog; with nothing else ready the shift ended, the coordinator froze the branch and opened a merge PR superseding the last one, and `-Continuous` started the next shift, which claimed BL-1305 again. This repeated every ~80 seconds: PRs #46 to #92, 103 CI runs, each new PR cancelling the previous one's CI so none could merge. Stopped by hand: the shift was killed, BL-1305 moved to Blocked (commit ffef7667f), stale runs cancelled, one PR (#93) opened.
- Three defects to fix in `RunDarkFactory.ps1` (read its header first; ADR-0029, ADR-0047 and its amendment 2 govern the frozen merge PR):
  1. **Bookkeeping-only freeze.** At shift end, compare the branch head with the head of the open frozen PR (or `master`); when every commit between them touches only `Tasks/`, keep the open PR and its CI instead of superseding it.
  2. **Requeue loop.** A task a lane moves Doing -> Backlog without a code commit should not be claimable again in the same shift (or should be held for a back-off, e.g. until `master` moves), so one task cannot spin a lane.
  3. **Continuous restart with no progress.** `-Continuous` should start the next shift only when the ending shift completed at least one task or `master`/the board changed in a way that makes new work ready; otherwise wait (e.g. for the open merge PR to merge) rather than restart immediately.
- Lanes should block, not requeue, a task that waits on something outside the shift (a merge, a CI run); consider making the requeue path say so.

## Acceptance criteria

- [ ] With an open frozen PR whose head differs from the shift branch only by commits touching `Tasks/`, the shift-end step leaves that PR open and opens none (shown by a test or a dry-run switch the script already has, or a new one, exercised in the task's Notes).
- [ ] A task requeued Doing -> Backlog without a non-`Tasks/` commit is not claimed again by any lane in the same shift.
- [ ] `-Continuous` does not start a new shift when the ending shift finished no task and nothing new became ready; it waits and logs why.
- [ ] `Documentation/Wiki/Dark-Factory-Recipes.md` and the script header describe the new behaviour.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-16: Created after the BL-1305 shift loop (PRs #46-#92); Stewart asked for it ("yes").
