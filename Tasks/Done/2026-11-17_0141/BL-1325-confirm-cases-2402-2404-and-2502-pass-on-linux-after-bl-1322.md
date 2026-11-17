---
id: BL-1325
title: Confirm cases 2402, 2404 and 2502 pass on Linux after BL-1322 and list them as passing
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1322]
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1325 — Confirm cases 2402, 2404 and 2502 pass on Linux after BL-1322 and list them as passing

## Goal

Cases 2402, 2404 and 2502 are confirmed passing on Linux and listed in the Linux passing file.

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] 2402, 2404 and 2502 appear in `Documentation/Conformance/upstream-test-cases-passing.linux-x64.txt`.

## Notes

CI run 44002670831 (commit 0cd51f95, includes the BL-1322 fix 2df5bfc9f): the Linux upstream-cases job passed and its artifact lists all three. Copied the artifact's list (1,325) and updated the Linux row in Upstream-Test-Case-Pass-Rate.md.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. No Linux CI run includes the BL-1322 fix yet (latest run 43979679184 predates it); needs the next master CI run on Linux, then list 2402/2404/2502 in upstream-test-cases-passing.linux-x64.txt
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Latest CI run 43979679184 (master 6023b4b) still predates the BL-1322 fix 2df5bfc9f; needs a newer master CI run on Linux, then list 2402/2404/2502 in upstream-test-cases-passing.linux-x64.txt
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. 2402, 2404, 2502 pass on Linux in CI run 44002670831
