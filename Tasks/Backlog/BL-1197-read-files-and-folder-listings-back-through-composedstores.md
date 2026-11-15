---
id: BL-1197
title: Read files and folder listings back through ComposedStores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1194]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1197 — Read files and folder listings back through ComposedStores

## Goal

`ComposedStores` returns a file's bytes and status and a folder's listing from the content store, as BL-1194's ADR names them, so every host can read back what a client uploaded.

## Context

- Decision: the ADR filed by BL-1194 (names, shapes, bounds, and whether hidden or confined names are readable).
- Where: `Application/Surl.Composition.UnitLibrary/ComposedStores.cs`, beside `SeedFileAsync` and `DeleteFileAsync`, which show how a request path becomes a `ContentPathMapping`. The content store (`Foundation/Surl.Content.UnitLibrary/ContentStore.cs`) has `GetFileStatus`, `GetEntryStatus` and `ListDirectory`, and the read path the HTTP server uses for `GET`.
- A read publishes nothing, asks no fault rule, and works on a `--directory` store. A path that escapes the served root is refused, as for seeding.
- Stand-in fit: the code under test uploads over FTP, SFTP, SMB, S3 or HTTP `PUT`, and the test reads the bytes back directly.

## Acceptance criteria

- [ ] `ComposedStores` has the content read members the ADR names, documented with XML comments.
- [ ] A test in `Application/Surl.Composition.UnitTests` seeds `/reports/a.txt` with `SeedFileAsync`, reads back its exact bytes and size, and lists `/reports` containing it.
- [ ] A test shows a missing path gives the ADR's not-found result and a `..` path is refused. A test shows a file over the ADR's bound is refused, not truncated.
- [ ] A test shows a read publishes no `StoreChangedEvent`.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests pass, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-15: Created.
