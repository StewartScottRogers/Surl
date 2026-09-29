---
id: BL-010
title: Implement the content store's file-system seam over System.IO
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-009]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-010 — Implement the content store's file-system seam over System.IO

## Goal

A production implementation of the content store's file-system seam (BL-008, BL-009)
over `System.IO` exists, so `Surl.Console` can serve a real directory. Integration tests
against a temporary directory prove it.

## Context

- The seam interface lives in `Surl.Content.UnitLibrary` (BL-008, extended in BL-009).
  This task adds its one real implementation beside it, as thin as it can be: each
  member is a direct call into `File`, `Directory`, `FileInfo`, `FileSystemInfo` or
  `FileStream`.
- Following symbolic links: `FileSystemInfo.ResolveLinkTarget(returnFinalTarget: true)`.
- Tests that touch the file system are `[TestCategory("Integration")]`
  (`.claude/rules/testing.md`), and `Measure-CodeQuality.ps1` measures coverage from the
  fast tests only. So each member of this class carries
  `[ExcludeFromCodeCoverage(Justification = "…")]` with a comment above it saying that
  the Integration tests in `Surl.Content.UnitTests` cover it. `Measure-CodeQuality.ps1`
  lists every exclusion, and one without a justifying comment is a finding.
- Tests pass on Windows, Linux and macOS. Creating a symbolic link on Windows needs a
  privilege, so the symbolic-link test runs only on Linux and macOS
  (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`).
- Use `Path.GetTempPath()` plus a unique folder per test, deleted in cleanup, because
  tests run in parallel at method level.

## Acceptance criteria

- [ ] One class in `Surl.Content.UnitLibrary` implements the seam over `System.IO`,
      named for what it does. It has no branching logic beyond what the seam contract
      requires.
- [ ] Every member carries `[ExcludeFromCodeCoverage]` with a justifying comment, or is
      covered by fast tests.
- [ ] `[TestCategory("Integration")]` tests in `Surl.Content.UnitTests` create a
      temporary tree and prove, through the content store: a file's length, last
      modification time and bytes; a range read; a directory reported as a directory; a
      missing path reported as nothing; and, on Linux and macOS only, a symbolic link
      pointing outside the root refused.
- [ ] `dotnet test --filter "FullyQualifiedName~Surl.Content"` (Integration included)
      is green on Windows.
- [ ] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Content.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
