---
id: BL-685
title: Record the object metadata index's byte format and BL-636's choices in ADR-0100
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-636]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-685 — Record the object metadata index's byte format and BL-636's choices in ADR-0100

## Goal

ADR-0100 decision 2.3 states the byte format of `<path>/.surl/objects/index` and the choices BL-636 made
building `ObjectMetadataIndex`, so the object-storage servers' ADRs (BL-417, BL-423, BL-429, BL-435) can cite
one decided source.

## Context

- BL-636 built `Foundation/Surl.Content.UnitLibrary/ObjectMetadataIndex.cs`. ADR-0100 decision 2.3 did not pin
  a byte format; BL-636 could not edit `Documentation/Planning/Decisions` because BL-641 held it, so the format
  and choices are recorded in BL-636's Notes, `ObjectMetadataIndexFormat`'s remarks and the library's
  `CLAUDE.md`. Copy them from there; the test
  `ObjectMetadataIndexTests.StoreAsync_PersistingIndex_WritesThePinnedBytes` pins the bytes.
- Choices to record (all "Decided by Claude under Stewart's delegation", BL-636): what counts toward
  `MaxMetadataBytes`; `MaxETagBytes` 1024; `NoSuchFile` outcome; stale entries dropped lazily (on read, and
  before calling the index full) rather than on every write; default content type `application/octet-stream`
  until BL-686 lands.

## Acceptance criteria

- [x] ADR-0100 decision 2.3 (or an amending ADR it links) states the index's byte format exactly as
      `ObjectMetadataIndexFormat`'s remarks give it, and the five choices listed under Context.

## Notes

- Recorded as ADR-0100 section 2.3.1, an amendment inside the ADR listed in its `Amended` header,
  rather than a new ADR: one place for the object servers' ADRs to cite. The byte format is copied
  from `ObjectMetadataIndexFormat`'s remarks and checked against its `Decode`; the five choices are
  copied from BL-636's Notes and checked against `ObjectMetadataIndex.cs`. The section also says
  lazy stale-dropping supersedes 2.3's "dropped on the next write".
- Docs only: no `.cs` or project file touched, so no build or test run was needed for this change.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0100 section 2.3.1 records the object metadata index's byte format and BL-636's five choices
