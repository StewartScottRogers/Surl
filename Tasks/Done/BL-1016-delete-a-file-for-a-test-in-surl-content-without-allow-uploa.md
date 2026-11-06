---
id: BL-1016
title: Delete a file for a test in Surl.Content without --allow-uploads
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-805]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-1016 — Delete a file for a test in Surl.Content without --allow-uploads

## Goal

`ContentStore.DeleteFileForTheTest(requestPath)` deletes a file as the test (ADR-0124 decision 7's
`DELETE /v1/files/{path}`): no `--allow-uploads` and no share write access needed, never asking the fault plan,
publishing one `StoreChange.Deleted`, and answering `ContentChangeResult.Absent` with nothing published when no
file is there - so the control endpoint (BL-1022) can delete what a test seeded.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decisions 3 and 7 (the store control and the control endpoint's file routes).
- Split from BL-816 (timed out after 120 min, 2026-11-06), which needed this beside its composition work. Reuse
  from the stash whose message is `darkfactory BL-816 20261106-033059` (commit
  `0b6c48207fcfb912c5b903ddc55ed2a06d93e6da`, called `S`; find its index with `git stash list`), with
  `git diff "S^" "S" -- Foundation/Surl.Content.UnitLibrary Foundation/Surl.Content.UnitTests | git apply`:
  `ContentStore.Control.cs` (+26, `DeleteFileForTheTest` over the existing `MapForTheTest`) and
  `ContentStoreTests.Control.cs` (+42). Neither file has changed on the shared branch since; if `git apply`
  refuses, re-make it by hand.
- Absent covers: nothing there, a folder, a path with a trailing `/`, a path under `/.surl`, or one hidden by the
  exposure options. A path no request may name throws `ArgumentException`.
- Stand-in: this is the content store's shared seed/reset surface; it publishes `StoreChange.Deleted` on the
  instance's event stream so a test observes the delete, and asks no fault (a test's own action is never faulted).

## Acceptance criteria

- [x] `DeleteFileForTheTest_WithUploadsOffInAReadOnlyShare_DeletesTheFileAndPublishesDeleted` passes.
- [x] `DeleteFileForTheTest_NoFileThere_IsAbsentAndPublishesNothing` and `DeleteFileForTheTest_ARefusedPath_Throws` pass.
- [x] Every existing `Surl.Content.UnitTests` test passes.
- [x] `dotnet build Foundation/Surl.Content.UnitTests -warnaserror` is clean, the fast tests are green, and
      `Surl.Content.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Reused the BL-816 stash commit 0b6c4820 as the task said; `git apply` took both hunks cleanly (ContentStore.Control.cs +26, ContentStoreTests.Control.cs +42), no hand edits needed.
- Verified: `dotnet build Foundation/Surl.Content.UnitTests -warnaserror` clean; Surl.Content.UnitTests 686 passed, 1 skipped; Surl.Content.UnitLibrary line-rate 1, branch-rate 1 (cobertura); full fast suite green.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-816 (timed out after 120 min); reuses its ContentStore change from stash `darkfactory BL-816 20261106-033059`.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ContentStore.DeleteFileForTheTest deletes a file as the test, publishing StoreChange.Deleted, without --allow-uploads or share write access
