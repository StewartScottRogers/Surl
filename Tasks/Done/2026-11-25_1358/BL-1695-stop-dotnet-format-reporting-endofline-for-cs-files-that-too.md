---
id: BL-1695
title: Stop dotnet format reporting ENDOFLINE for .cs files that tools write with LF
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/verify/SKILL.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1695 — Stop dotnet format reporting ENDOFLINE for .cs files that tools write with LF

## Goal

The `verify` skill says that `ENDOFLINE` errors which leave `git diff` empty after `dotnet format` are not drift in the repository, so no run files a task for them.

## Context

Found by BL-1686 on 2026-11-25 (dark factory lane 1). A `.cs` file written with LF stays LF in that
worktree until Git checks it out again, while `.editorconfig` asks for CRLF, so
`dotnet format --verify-no-changes` reports `ENDOFLINE` for it. The index stores LF either way
(`.gitattributes`: `* text=auto`), so `dotnet format` leaves nothing to commit. BL-1686 saw 11,633 such
errors and an empty diff. Start at `.claude/skills/verify/SKILL.md` step 3. BL-1694 asks for the same
command to pass and is satisfied the same way.

## Acceptance criteria

- [x] `.claude/skills/verify/SKILL.md` states that `ENDOFLINE`-only errors with an empty `git diff` after `dotnet format` are worktree line endings, with nothing to commit and no task to file.

## Notes

- 2026-11-25, lane 1: added the statement to step 3 of the `verify` skill. It also covers a diff that is "the same as
  before" `dotnet format`, because verify usually runs with uncommitted work, where `git diff` is never empty.
- Verify after the edit: restore up to date, build 0 warnings and 0 errors, `dotnet format --verify-no-changes`
  exit 0, fast tests 28,945 passed, 0 failed, 3 skipped, no files reformatted.
- BL-1696 (Backlog) still covers preventing the errors; this task only documents them.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The verify skill says ENDOFLINE-only format errors that leave no diff are worktree line endings: nothing to commit, no task to file
