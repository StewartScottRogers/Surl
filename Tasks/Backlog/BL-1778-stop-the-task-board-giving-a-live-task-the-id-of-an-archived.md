---
id: BL-1778
title: Stop the task board giving a live task the ID of an archived task
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board]
requirement: none
created: 2026-11-25
completed:
---
# BL-1778 — Stop the task board giving a live task the ID of an archived task

## Goal

The dedupe command renumbers a live task that holds the ID of an archived task.

## Context

- On 2026-11-25 two tasks held BL-1773: one archived in Tasks/Done/2026-11-25_2312, one filed by another lane and
  later claimed. The dedupe command of task-board.ps1 groups only tasks that are not archived, so it never saw the pair.
- The archived file is never edited; the live one takes the next free ID. Get-DoneIds counts the archived one, so a
  task depending on the live one would look ready.

## Acceptance criteria

- [ ] dedupe -Since <ref>, run on a copy of the board with one archived and one live task sharing an ID, renumbers the live one only; the task notes the command and its output.
- [ ] SKILL.md's dedupe row says what it does with an archived duplicate.

## Notes

## Log

- 2026-11-25: Created.
