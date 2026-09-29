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
completed: 2026-09-29
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

- [x] `ContentStore(string, IContentFileSystem)` is removed, or kept with a doc comment
      that is true of the code as it is now; the choice and why are under Notes.
- [x] `ContentExposureOptions.ServeEverythingInsideTheRoot` is removed or its doc comment
      no longer names callers that do not exist.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

Filed by BL-070.

- 2026-09-29, choice: **kept** `ContentStore(string, IContentFileSystem)` and
  `ContentExposureOptions.ServeEverythingInsideTheRoot`, with doc comments rewritten to be
  true now. Why: no production code calls the constructor (`Surl.Console` uses the
  three-argument one via `ComposeContentStore`), but the HTTP, Gopher, DICT and TFTP test
  projects still do, ~20 call sites. Removing it would reach outside this task's `touches`
  into four protocol test projects plus DICT's library decision - exactly BL-069's scope,
  which already owns the removal. The comments now say no production caller exists, name
  the test callers, and name BL-069 as the task that removes both.
- The Goal's "no document says a caller still serves with it" stays partly open until
  BL-069: the tests really do still serve with it, and the comment now says so honestly.
- ADR-0015 section 8 describes the state at BL-047 (a historical decision record); left
  as written, outside `touches`.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ContentStore's two-argument constructor and ServeEverythingInsideTheRoot now document truthfully that only tests use them, and BL-069 removes them
