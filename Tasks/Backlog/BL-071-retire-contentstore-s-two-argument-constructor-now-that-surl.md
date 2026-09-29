---
id: BL-071
title: Retire ContentStore's two-argument constructor now that surl passes exposure options
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-070]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-071 — Retire ContentStore's two-argument constructor now that surl passes exposure options

## Goal

`ContentStore` has one constructor, `ContentStore(string, IContentFileSystem, ContentExposureOptions)`,
and no document says a caller still serves with `ServeEverythingInsideTheRoot`.

## Context

- BL-070 made `Surl.Console/CommandLineRunner.cs` build its store with the three-argument
  constructor; no production code calls `ContentStore(string, IContentFileSystem)` any more.
- `ContentExposureOptions.ServeEverythingInsideTheRoot`'s doc comment says it is "kept for
  the callers of `ContentStore(string, IContentFileSystem)` until each passes the options
  `Surl.Cli` parses" - that is no longer true of production code.
- Tests in `Surl.Content.UnitTests` that use the two-argument constructor pass
  `ContentExposureOptions.ServeEverythingInsideTheRoot` (or the options they need) explicitly.

## Acceptance criteria

- [ ] `ContentStore(string, IContentFileSystem)` is removed, or kept with a doc comment
      that is true of the code as it is now; the choice and why are under Notes.
- [ ] `ContentExposureOptions.ServeEverythingInsideTheRoot` is removed or its doc comment
      no longer names callers that do not exist.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

Filed by BL-070.

## Log

- 2026-09-28: Created.
