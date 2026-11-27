---
id: BL-1862
title: Remove the stale Phase 1 line from the content plus serving engine library notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary/CLAUDE.md, Foundation/Surl.Core.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1862 — Remove the stale Phase 1 line from the content plus serving engine library notes

## Goal

The `CLAUDE.md` files of `Surl.Content.UnitLibrary` and `Surl.Core.UnitLibrary` no longer open with a bare "Phase 1." line that reads as the library's scope.

## Context

Found while working BL-1828, which removed the same line from `Application/Surl.Cli.UnitLibrary/CLAUDE.md`. Line 3 of each file is "Phase 1.", while each library holds later work (`Documentation/Product/Product-Overview.md`, "Phasing"):

- `Foundation/Surl.Content.UnitLibrary/CLAUDE.md`: the library holds the content changes for FTP, SCP and SFTP (Phase 2) and later content work.
- `Foundation/Surl.Core.UnitLibrary/CLAUDE.md`: the library holds the event stream and fault plan (ADR-0124), added after Phase 1.

Remove the line only; add no replacement phase list. `Phase N.` lines followed by other text, in other libraries, are out of scope. No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] Neither file contains a line that reads only "Phase 1.".
- [ ] Each file's title is followed by one blank line and then its first descriptive paragraph.
- [ ] No other line of either file changes.

## Notes

## Log

- 2026-11-27: Created.
