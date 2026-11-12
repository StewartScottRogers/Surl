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
completed: 2026-11-11
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

- [x] Named tests in `Application/Surl.Composition.UnitTests` show shares, buckets and access (with an `@group`)
      reaching the composed `ContentStore`, one instance handed to every file server, and `protocols=ftp` admitting
      `ftps`.
- [x] Named tests show a missing share path refused at start with the ADR's exit code and text, and each widening
      setting's warning written.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

- 2026-11-11 (lane 3): built `ContentShareComposition` (`Compose`, `WithTlsTwins`, `WriteWarnings`,
  `WriteStartLines`), a six-argument `ComposeContentStore` overload taking shares and buckets, and two optional
  seams, `SurlServerCompositionSeams.CanOpenShareDirectory` (default `DataDirectoryProbe.CanOpen`) and
  `ShareFileSystem` (default `DiskContentFileSystem`). The content store is now composed after authentication and
  groups, so `--share-access` groups expand (ADR-0102 decision 5's order). Tests: `ContentShareCompositionTests`
  and `SurlServerCompositionTests.Shares.cs` (every file server - HTTP, FTP, TFTP, SMB, Gopher - holds the one
  store with the share; a missing share path `(37)`; the `@everyone` warning and `-v` notes). Gates: build
  `-warnaserror` clean, all 79 fast test projects green (`Surl.Composition.UnitTests` 376/376), and
  `ContentShareComposition` and every changed line at 100% line and branch.
- Choices (sensible defaults, decided here):
  - Refusal texts ADR-0101 row 10 leaves to composition: `(2) option --share: share <name> is not a valid folder
    name`, `(2) option --bucket: bucket <name> is not a valid folder name`, and `(2) option --bucket: bucket
    <name> maps to <path>, which is not a content path`.
  - Overlap compares full paths ignoring case on every platform: refusing a case-only difference on Linux costs
    nothing, while missing one on Windows or macOS would reach one file under two sets of access rules.
  - A member reached twice (directly and through a group, or in two rows) keeps the wider access, as
    `ContentShare.AccessFor` takes the wider of the caller's and `@everyone`'s.
  - TLS twins are added as `Account` compares them (`x`, `x`+`s`, `x` less a trailing `s`); the extra names no
    server answers (`gopherss`, `dn`) are harmless, and `ws`/`wss` stays right.
  - A bucket's record path is percent-decoded; each segment is encoded again for `ContentBucket`, which reads a
    request path.
  - A `--share-access` naming no `--share` is ignored here (the parser refuses it from BL-989 on, through
    `ConfiguredContentConflicts`); it opens nothing.
- Follow-up filed: BL-1058 (check or create each `--bucket` folder at start, ADR-0102 decision 2.4), kept out to
  hold this task to its size.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Shares, share access (groups and @everyone expanded) and buckets reach the one content store every file server holds; a missing share path is refused (37) and @everyone is warned
