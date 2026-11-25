---
id: BL-1652
title: Restore the commit hash a stray BEL character corrupted in ADR-0053, ADR-0055 and ADR-0056
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0056-how-the-pop3-server-answers-upstream-curl.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1652 — Restore the commit hash a stray BEL character corrupted in ADR-0053, ADR-0055 and ADR-0056

## Goal

The "Recorded by BL-1291" line in each of the three ADRs names commit
`a06ae86113e511d6d6204949c1fbf80e29b41ba9` in plain text, and the files read as text.

## Context

Each line (ADR-0053:420, ADR-0055:656, ADR-0056:382) holds a BEL (0x07) where `a` should
be - a PowerShell `` `a `` escape - so the hash reads `^G06ae861...`, `file` calls the ADRs
"data" and `grep` treats them as binary. `git cat-file -t a06ae86113e5...` confirms the
intended commit exists. Found 2026-11-24.

## Acceptance criteria

- [x] `grep -lP "\x07" Documentation/Planning/Decisions/*.md` prints nothing.
- [x] Each of the three lines contains `commit a06ae86113e511d6d6204949c1fbf80e29b41ba9`.
- [x] No other byte in the three files changes (`git diff --stat` shows one line each).

## Notes

`Tasks/Done/2026-11-22_2230/BL-1608-*.md` holds a BEL too, but archived tasks are final;
leave it.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. BEL replaced by 'a' in the three ADRs; no BEL remains in Decisions
