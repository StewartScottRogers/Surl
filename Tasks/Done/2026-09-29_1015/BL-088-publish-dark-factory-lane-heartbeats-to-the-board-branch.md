---
id: BL-088
title: Publish dark factory lane heartbeats to the board branch
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-088 — Publish dark factory lane heartbeats to the board branch

## Goal

The live task board at https://stewartscottrogers.github.io/Surl/board/ shows one card per
dark factory lane, because `RunDarkFactory.ps1` publishes `status.json` to the `board` branch.

## Context

ADR-0029 adopts the Curl port's board page and its `status.json` schema 1 unchanged
(the Curl port's ADR-0129, items 7 to 9). The page is published; the lane side is not:
Surl's `RunDarkFactory.ps1` writes no heartbeat, so the page says no status is published.
The Curl port's `RunDarkFactory.ps1` (github.com/StewartScottRogers/Curl, master) already
does this - `-HeartbeatMinutes`, `lane-<n>.heartbeat.json` in the shift's lane state folder,
`Get-BoardStatusJson`, `New-BoardCommit`, `Publish-BoardStatus`,
`Publish-BoardStatusIfDue` and its self-test - and can be copied, since this is tooling,
not a Surl behaviour (ADR-0003 allows copying code).

## Acceptance criteria

- [x] `RunDarkFactory.ps1` has a `-HeartbeatMinutes` parameter (default 3, 0 = off).
- [x] Each lane (lane `0` for a single-runner shift) writes `lane-<n>.heartbeat.json`
      atomically on every phase change and at least every 60 seconds during a run.
- [x] The coordinator alone merges the heartbeats into `status.json` schema 1 and
      force-pushes it to `board` as one parentless commit built with plumbing, every
      `-HeartbeatMinutes` and once more at shift end with `"state": "ended"`; a failed
      push is traced and never stops the shift.
- [x] The script's self-test builds `status.json` from three made-up lanes without pushing,
      and its output matches the schema in ADR-0029.

## Notes

- Copied from the Curl port's `RunDarkFactory.ps1` (origin/master, 2026-09-29): the
  heartbeat and board-branch sections, `-TestHeartbeat`, and the call sites in the claim,
  run, integrate, lock, token-wait and shift-end paths. ADR-0129 references became
  ADR-0029 item 6. Left out: the Curl port's `-Lanes Auto` pieces, which Surl does not have,
  so `$WritesHeartbeat` is `$Lane -or $Lanes -le 1` (minus the out-of-tokens rehearsal).
- Lanes are not passed `-HeartbeatMinutes`: they never publish. `-Continuous` forwards it
  to the next shift.
- `-TestHeartbeat` output checked: five lane-1 files through starting/claim/run/integrate/
  finished, then status.json with lanes 1, 2, 3 sorted, the same fields as
  `.github/board/fixtures/status.json`, and a parentless commit whose tree holds only
  `status.json` (checked with `git cat-file -p` / `git ls-tree`). Nothing pushed.
- ADR-0029's Consequences bullet that said the heartbeats were not yet written was updated
  to say what the script now does.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. RunDarkFactory.ps1 writes lane heartbeats and publishes status.json to the board branch every -HeartbeatMinutes and at shift end
