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
completed:
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

- [ ] `dotnet format --verify-no-changes` exits 0.
- [ ] The diff changes formatting only: line endings, `using` order, whitespace and final newlines.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
