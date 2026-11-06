---
id: BL-988
title: Compose configured shares, share access and buckets into the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-987, BL-657, BL-658, BL-659, BL-660, BL-661, BL-662]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-988 — Compose configured shares, share access and buckets into the content store

## Goal

The shares, share access and buckets in `SurlCommandLine` (from BL-987) reach the one `ContentStore` every file
server and object-storage server shares, with each share's path checked at start and each widening setting warned.

## Context

- Second of the three tasks BL-663 was split into (see BL-987).
- Decisions: ADR-0101 (which setting widens access, so is warned at start per ADR-0032 section 9) and ADR-0102
  decisions 2, 4 and 5.
- Builds on: BL-655 (shares and buckets in the store), BL-656 (access checks), BL-657 to BL-662 (every original
  file server passes its caller), BL-646 (group expansion).
- Where: `ComposeContentStore` in `Application/Surl.Composition.UnitLibrary` (after BL-609/BL-614): each share's path
  checked at start (missing or not a directory refused with the ADR's exit code and text, as `--directory` is by
  `DataDirectoryProbe`, ADR-0031 decision 3); the share table and access table passed to `ContentStore`.
  `protocols=` composed into `ContentShare.Schemes` with each TLS twin (ADR-0102 decision 2.1; see BL-987).
- The extended object-storage servers (S3, Azure, Cloud Storage, OCI) receive the same store when their "Serve …
  from the surl executable" tasks register them; nothing here waits on them.
- Stand-in: the content store is the shared store; its seeding and events are BL-805's and ADR-0127's.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Composition.UnitTests` show shares, buckets and access (with an `@group`)
      reaching the composed `ContentStore`, one instance handed to every file server, and `protocols=ftp` admitting
      `ftps`.
- [ ] Named tests show a missing share path refused at start with the ADR's exit code and text, and each widening
      setting's warning written.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
