---
id: BL-1345
title: Confirm case 2407 passes on Linux after the bare-LF reply fix and record it in the pass rate
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1336]
touches: [Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md]
requirement: none
created: 2026-11-17
completed:
---
# BL-1345 — Confirm case 2407 passes on Linux after the bare-LF reply fix and record it in the pass rate

## Goal

The Linux upstream test cases run after BL-1336 lists `test2407` as passing, and the pass-rate document says so.

## Context

BL-1336 found the cause: case 2407's reply part has bare-LF lines (no `crlf="headers"`) and the `http` half keeps the connection open, so the gateway never saw the head end and waited forever. It now accepts bare LF (`NghttpxGatewayReplyHead`). Only a Linux CI run on the shift branch or master after the fix can confirm it; the libtest cases are not run on Windows.

## Acceptance criteria

- [ ] A Linux `Upstream test cases` run that includes BL-1336's commit lists `test2407` in `upstream-test-cases-passing.linux-x64.txt` (if it still times out, file a task from its timeout output).
- [ ] `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` says so, with the run link.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Blocked. Waiting for run:44068385591 (ci.yml on factory/phase-1 at 4ac7b2c7b, contains the BL-1336 fix)
