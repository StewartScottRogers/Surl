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
completed:
---
# BL-1325 — Confirm cases 2402, 2404 and 2502 pass on Linux after BL-1322 and list them as passing

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. No Linux CI run includes the BL-1322 fix yet (latest run 43979679184 predates it); needs the next master CI run on Linux, then list 2402/2404/2502 in upstream-test-cases-passing.linux-x64.txt
