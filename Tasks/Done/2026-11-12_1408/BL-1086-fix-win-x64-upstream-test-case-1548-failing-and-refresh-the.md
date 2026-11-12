---
id: BL-1086
title: Fix win-x64 upstream test case 1548 failing and refresh the passing list
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-570]
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1086 — Fix win-x64 upstream test case 1548 failing and refresh the passing list

## Goal

The Windows upstream test-case run passes its guard: case 1548 passes again and the passing list names every case that passes.

## Context

- Found in BL-570 (2026-11-12, lane 3): `UpstreamTestCasesAgainstSurlTests.RunAsync_EveryCaseWithAComposedServerHalf_WritesOneResultLinePerCase` fails on win-x64 twice in a row: `test1548 fail errorcode`, and 30, 37 and 1297 pass but are not in `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- BL-570 only flips the `dns` row and adds a datagram listener, and its three cases skip; whether it caused this was not measured against a clean checkout. Start by running the test at the commit before BL-570's.
- The pinned win-x64 reference curl may have changed (a Git update replaces it; see the project memory "Windows reference curl lost").

## Acceptance criteria

- [x] The cause of 1548's `errorcode` failure is found and recorded in Notes.
- [x] The test above passes on win-x64, with 30, 37 and 1297 added to the passing list and the win-x64 counts in `Upstream-Test-Case-Pass-Rate.md` raised if they pass.

## Notes

- 2026-11-12 (lane 4): On a clean checkout at d7b964fc (without BL-570, commit f8fb6a37) the test passes on win-x64: 1548 passes and is in the passing list; 30, 37 and 1297 are not passing there. So the failure is not pre-existing: it appears only with BL-570 (the dns row flip or datagram listener). Fix belongs with BL-570 (or after it lands): re-run the test there, find why 1548 returns errorcode, then add any newly passing cases to the list and raise the win-x64 counts.

- 2026-11-12 (lane 1): Cause: BL-570 (f8fb6a37) exposed a regression where the HTTP server closed the connection on an empty reply part, so test 1548 returned an errorcode failure. BL-1078 (3fe3c1a6) fixed it. On current HEAD the guard test passes on win-x64 (1 passed, 2m10s): 1548 is in the passing list, and 30, 37 and 1297 do not pass now (the guard would fail otherwise), so the list and counts need no change.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Backlog. Clean checkout passes; failure only appears with BL-570 (f8fb6a37), so it waits on BL-570 landing
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Guard test passes on win-x64 at HEAD; 1548 failure was fixed by BL-1078; 30/37/1297 do not pass so the list is unchanged
