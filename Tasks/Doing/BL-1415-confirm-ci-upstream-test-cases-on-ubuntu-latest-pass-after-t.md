---
id: BL-1415
title: Confirm CI upstream test cases on ubuntu-latest pass after the BL-1414 port-collision fix
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1414]
touches: []
requirement: none
created: 2026-11-20
completed:
---
# BL-1415 — Confirm CI upstream test cases on ubuntu-latest pass after the BL-1414 port-collision fix

## Goal

A dispatched `ci` run on the shift branch passes "Upstream test cases (ubuntu-latest)" with the BL-1414 fix pushed.

## Context

BL-1414 fixed the intermittent failure of upstream test case 2090 on linux-x64 (a port-number collision between
an IPv4-only TLS listener and a `::1` plaintext listener, see `UpstreamTestCaseServerHalves.StartAsync`). The
lane that did it may not push, so the CI run it wanted could not be dispatched with the fix. Once the fix is on
the shift branch, dispatch it.

## Acceptance criteria

- [ ] `.claude/skills/task-board/dispatch-workflow.ps1 -Id BL-1415 -Workflow ci -Ref <shift branch>` ran after BL-1414's commit was pushed, and its run id is in the Log.
- [ ] That run's job "Upstream test cases (ubuntu-latest)" passed.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Blocked. Waiting for run:44745192387 (ci on factory/phase-1, includes BL-1414 fix)
- 2026-11-20: Blocked -> Backlog. run:44745192387 (ci on factory/phase-1 with the BL-1414 fix) completed success: Upstream test cases (ubuntu-latest) and (macos-latest) passed, as did Build and test on all three
- 2026-11-20: Backlog -> Doing.
