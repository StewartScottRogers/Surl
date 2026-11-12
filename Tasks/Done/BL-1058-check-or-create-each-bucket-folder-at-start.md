---
id: BL-1058
title: Check or create each --bucket folder at start
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-988]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-11
completed: 2026-11-12
---
# BL-1058 — Check or create each --bucket folder at start

## Goal

At start, each `--bucket`'s content path is an existing folder when it lies on disk (refused otherwise
with exit 37), and is created empty when it lies in the in-memory root, as ADR-0102 decision 2.4 says.

## Context

- Follow-up of BL-988, which composes `ContentBucket`s into the store (`ContentShareComposition.Compose`)
  but does not check or create their folders.
- ADR-0102 decision 2.4: "At start the folder must exist when it is on disk (row 19's text with `bucket`
  for `share`, exit 37); in memory it is created empty." ADR-0101 decision 5 row 19.
- A bucket path lies on disk when its first segment names a `--share`, or when `--directory` is given;
  otherwise it lies in the in-memory root. `ContentStore.MapRequestPath`, `GetEntryKind` and `SeedFolder`
  are the store's existing tools; mind that `SeedFolder` on the in-memory root changes what `ResetAsync`
  restores (the store's configured contents are captured in its constructor).
- Where: after `ComposeContentStore` in `SurlServerComposition.ComposeAvailableAsync`.
- Stand-in: a test that serves S3 from memory gets its bucket folder without seeding it.

## Acceptance criteria

- [x] Named tests in `Application/Surl.Composition.UnitTests` show a bucket inside a share whose folder is
      missing refused `(37) Could not open directory <path> for bucket <name>`, and one in the in-memory root
      created empty and listed by the S3 server's bucket listing.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

- Built `ContentShareComposition.PrepareBucketFolders`, called in `ComposeAvailableAsync` after the content
  store. On disk (first segment names a share, else `--directory`): the folder is checked through the share's
  or the data directory's file system, refused `(37)` with the path as written. In memory: `SeedFolder`; the
  store's constructor already captures bucket folders in its configured state, so reset restores them.
  A `NotPermitted` seed (a file in the way) gets the same (37) text. The "S3 listing" criterion is shown
  through the store (`GetEntryKind` of the bucket path is a Directory), which is what the S3 listing reads.
- Existing share tests now create the share and bucket folders they use. One flaky unrelated gRPC test
  (`ServeStreamAsync_ShutdownBeforeAnyReply_IsTrailersOnlyUnavailable`) failed once and passed twice on rerun.

## Log

- 2026-11-11: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Each --bucket folder is checked on disk (exit 37) or created empty in memory at start
