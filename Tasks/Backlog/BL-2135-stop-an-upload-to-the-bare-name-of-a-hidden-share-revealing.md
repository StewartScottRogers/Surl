---
id: BL-2135
title: Stop an upload to the bare name of a hidden share revealing that the share exists
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2135 — Stop an upload to the bare name of a hidden share revealing that the share exists

## Goal

An upload to the bare name of a share the caller may not read ends the same way as an upload to a name that does not exist.

## Context

Noticed from reading the source while working BL-2133 on 2026-11-28; not run, so write the failing test first.
ADR-0102 decision 2.3: a share the caller may not read is answered as a path that does not exist, and a peer cannot
learn that it exists. `ContentStore.Map` answers such a share `ContentPathMapping.AnsweredAsAbsent` over
`<root>/<share>`, and `ContentStore.UploadOpeningResult` then answers `NotPermitted` for it, where a missing
top-level name under `--allow-uploads` is opened. So `PUT /team` appears to get `403` while `PUT /nothing` gets
`201`. Paths below the share appear to end like missing paths unless the root holds a folder of the same name. The
share shadows the root entry of its name, so the fix needs a decision on what both uploads get: record it in an ADR
("Decided by Claude under Stewart's delegation"). Check every caller of the upload opening and the content store's
other writes (create folder, rename) for the same difference.

## Acceptance criteria

- [ ] A `Surl.Content.UnitTests` test shows that, with uploads allowed, an upload to the bare name of a share the caller may not read and an upload to a missing top-level name give the same result.
- [ ] A test shows the same, or a recorded reason why not, for creating a folder and for renaming onto that name.
- [ ] A test shows the share's own content is unchanged by any of those writes.
- [ ] An ADR records what both writes get and why.

## Notes

## Log

- 2026-11-28: Created.
