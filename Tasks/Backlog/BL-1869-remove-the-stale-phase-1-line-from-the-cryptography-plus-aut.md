---
id: BL-1869
title: Remove the stale Phase 1 line from the cryptography plus authentication library notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Cryptography/Surl.Cryptography.UnitLibrary/CLAUDE.md, Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1869 — Remove the stale Phase 1 line from the cryptography plus authentication library notes

## Goal

The `CLAUDE.md` files of `Surl.Cryptography.UnitLibrary` and `Surl.Authentication.UnitLibrary` no longer open with a bare "Phase 1." line that reads as the library's scope.

## Context

Found while working BL-1828, which removed the same line from `Application/Surl.Cli.UnitLibrary/CLAUDE.md`. Line 3 of each file is "Phase 1.", while each library holds later work (`Documentation/Product/Product-Overview.md`, "Phasing"):

- `Cryptography/Surl.Cryptography.UnitLibrary/CLAUDE.md`: the library holds `Des` for NTLMv1 (BL-291), done after Phase 1.
- `Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md`: the library holds SSH logins (Phase 2), the mail servers' SASL mechanisms (Phase 3) and the extended servers' logins.

Remove the line only; add no replacement phase list. `Phase N.` lines followed by other text, in other libraries, are out of scope. No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] Neither file contains a line that reads only "Phase 1.".
- [ ] Each file's title is followed by one blank line and then its first descriptive paragraph.
- [ ] No other line of either file changes.

## Notes

## Log

- 2026-11-27: Created.
