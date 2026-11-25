---
id: BL-1696
title: Keep dotnet format's end-of-line check passing in checkouts where tools write LF files
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [.gitattributes, .claude/hooks, .claude/settings.json, .claude/skills/verify]
requirement: none
created: 2026-11-25
completed:
---
# BL-1696 — Keep dotnet format's end-of-line check passing in checkouts where tools write LF files

## Goal

A `.cs` file an agent writes with LF line endings does not make `dotnet format --verify-no-changes` fail in that
checkout.

## Context

BL-1693 and BL-1694, 2026-11-25. `.editorconfig` asks for `end_of_line = crlf`, `.gitattributes` has `* text=auto`
and Stewart's machine has `core.autocrlf true`, so the index holds LF and a fresh Windows checkout holds CRLF and
passes. A file written with LF stays LF in that working tree until git rewrites it, and the check reports `ENDOFLINE`
on it (`git ls-files --eol` shows `i/lf w/lf`). Candidates: a hook that writes CRLF after an edit, a step in the
`verify` skill, or `*.cs text eol=crlf` in `.gitattributes`. The last one also changes Linux and macOS checkouts, and
with them the line breaks inside C# raw and verbatim string literals there, so it needs a green CI run on all three.

## Acceptance criteria

- [ ] In a checkout holding a tracked `.cs` file rewritten with LF endings, the documented verify steps pass or turn it into CRLF; the Notes show the commands and their output.
- [ ] The chosen fix and the reason for it are recorded in the Notes.
- [ ] If `.gitattributes` changes, the `CI` workflow passes on Windows, Linux and macOS for the commit.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
