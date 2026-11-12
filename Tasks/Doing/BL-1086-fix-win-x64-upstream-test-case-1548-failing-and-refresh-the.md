---
id: BL-1086
title: Fix win-x64 upstream test case 1548 failing and refresh the passing list
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-12
completed:
---
# BL-1086 — Fix win-x64 upstream test case 1548 failing and refresh the passing list

## Goal

The Windows upstream test-case run passes its guard: case 1548 passes again and the passing list names every case that passes.

## Context

- Found in BL-570 (2026-11-12, lane 3): `UpstreamTestCasesAgainstSurlTests.RunAsync_EveryCaseWithAComposedServerHalf_WritesOneResultLinePerCase` fails on win-x64 twice in a row: `test1548 fail errorcode`, and 30, 37 and 1297 pass but are not in `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- BL-570 only flips the `dns` row and adds a datagram listener, and its three cases skip; whether it caused this was not measured against a clean checkout. Start by running the test at the commit before BL-570's.
- The pinned win-x64 reference curl may have changed (a Git update replaces it; see the project memory "Windows reference curl lost").

## Acceptance criteria

- [ ] The cause of 1548's `errorcode` failure is found and recorded in Notes.
- [ ] The test above passes on win-x64, with 30, 37 and 1297 added to the passing list and the win-x64 counts in `Upstream-Test-Case-Pass-Rate.md` raised if they pass.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
