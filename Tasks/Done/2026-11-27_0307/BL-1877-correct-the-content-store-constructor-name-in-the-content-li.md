---
id: BL-1877
title: Correct the content store constructor name in the content library notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1877 — Correct the content store constructor name in the content library notes

## Goal

`Foundation/Surl.Content.UnitLibrary/CLAUDE.md` names the `ContentStore` constructor that takes shares, buckets, events and faults by a description that matches its parameter list.

## Context

Found while working BL-1870. The notes say "`ContentStore`'s five-argument constructor" twice: in the "Named shares and buckets" paragraph and in the "Stand-in events and faults" paragraph. The constructor in `Foundation/Surl.Content.UnitLibrary/ContentStore.cs` has five required parameters (`servedRoot`, `fileSystem`, `exposureOptions`, `shares`, `buckets`) and three optional ones (`events`, `faults`, `sweepClock`), so it takes up to eight arguments. The second paragraph also omits `sweepClock`; read the constructor's doc comment before describing it. ADR-0129 uses the same phrase as a record of its decision and is out of scope. Documentation only.

## Acceptance criteria

- [x] The file no longer contains the text "five-argument constructor".
- [x] Each of the two paragraphs describes the constructor by parameters that exist in `ContentStore.cs`, saying which are optional where it mentions optional ones.
- [x] Only `Foundation/Surl.Content.UnitLibrary/CLAUDE.md` changes, apart from this task file.

## Notes

- Both paragraphs now name the constructor by its `shares` and `buckets` parameters; the events
  paragraph names the optional `events`, `faults` and `sweepClock`.
- The notes say the sweep runs before the configured state is captured, which is the order in the
  code. The `sweepClock` doc comment in `ContentStore.cs` says the constructor "first calls" the
  sweep, though argument checks and share setup come before it. Left as it is: code files are
  outside this task, and no task was filed for a one-word nuance with no effect on callers.
- Docs only. `dotnet build` clean (0 warnings, 0 errors); fast tests passed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The content library notes name the ContentStore constructor by its real parameters, including optional events, faults and sweepClock
