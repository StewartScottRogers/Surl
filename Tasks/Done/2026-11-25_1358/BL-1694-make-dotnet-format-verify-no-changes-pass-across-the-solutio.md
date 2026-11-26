---
id: BL-1694
title: Make dotnet format --verify-no-changes pass across the solution
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: []
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1694 — Make dotnet format --verify-no-changes pass across the solution

## Goal

`dotnet format --verify-no-changes` at the repository root exits 0.

## Context

Found by the verify gate of BL-1690 on 2026-11-25: the check fails on about 100 `.cs` files across
`Application/`, `Conformance/`, `Foundation/` and `Protocols/`, with ENDOFLINE (LF where `.editorconfig`
asks for CRLF; the Imap, Pop3 and Smtp libraries hold most of them), IMPORTS, WHITESPACE and
FINALNEWLINE. `touches` is empty on purpose: the change spans many projects, so the task runs alone.

## Acceptance criteria

- [x] `dotnet format --verify-no-changes` exits 0.
- [x] The diff changes formatting only: line endings, `using` order, whitespace and final newlines.
- [x] Build clean and fast tests green.

## Notes

- 2026-11-25: No source change was needed. At `7709f3c9c`, `dotnet format --verify-no-changes` on lane 1 exits 0:
  `Formatted 0 of 5973 files`.
- `IMPORTS`, `WHITESPACE` and `FINALNEWLINE` were fixed by BL-1693 (`08ac093fa`). This task's diff holds no source
  file, so the second criterion holds with nothing to change.
- `ENDOFLINE`: all 5288 tracked `.cs` files here are `i/lf w/crlf`. The error appears only on files that are LF in a
  working tree (`i/lf w/lf`), which no commit can change. The main checkout had 4 such files on 2026-11-25; they were
  left alone, because a lane works only in its own checkout. Preventing it is BL-1696.
- Verify: build 0 warnings, 0 errors; fast tests 28945 passed, 0 failed, 3 skipped. Integration tests not run,
  because no code changed. No design decision was taken, so no ADR.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. dotnet format --verify-no-changes exits 0 on a CRLF checkout; BL-1693 had fixed the content errors, LF-file prevention filed as BL-1696
