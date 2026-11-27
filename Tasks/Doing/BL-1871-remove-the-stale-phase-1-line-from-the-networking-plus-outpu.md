---
id: BL-1871
title: Remove the stale Phase 1 line from the networking plus output library notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary/CLAUDE.md, Foundation/Surl.Output.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1871 — Remove the stale Phase 1 line from the networking plus output library notes

## Goal

The `CLAUDE.md` files of `Surl.Networking.UnitLibrary` and `Surl.Output.UnitLibrary` no longer open with a bare "Phase 1." line that reads as the library's scope.

## Context

Found while working BL-1828, which removed the same line from `Application/Surl.Cli.UnitLibrary/CLAUDE.md`. Line 3 of each file is "Phase 1.", while each library holds later work (`Documentation/Product/Product-Overview.md`, "Phasing"):

- `Foundation/Surl.Networking.UnitLibrary/CLAUDE.md`: the library holds FTP data connections (Phase 2), the proxy's origin connections and TLS-SRP, added after Phase 1. BL-1849 corrects another statement in this file; the board keeps the two apart through `touches`.
- `Foundation/Surl.Output.UnitLibrary/CLAUDE.md`: the library has grown since Phase 1; its listener status line covers Unix sockets (ADR-0094), for one.

Check the Output claim against the file and its `.cs` files before removing the line; if the library holds nothing later than Phase 1, keep its line and say why in Notes. Remove the line only; add no replacement phase list. `Phase N.` lines followed by other text, in other libraries, are out of scope. No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The Networking file contains no line that reads only "Phase 1.".
- [ ] The Output file contains no such line, or Notes states why its line is still true.
- [ ] Where a line is removed, the title is followed by one blank line and then the first descriptive paragraph.
- [ ] This task changes no other line of either file.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
