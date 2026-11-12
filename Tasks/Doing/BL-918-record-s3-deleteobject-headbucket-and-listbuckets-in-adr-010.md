---
id: BL-918
title: Record S3 DeleteObject, HeadBucket and ListBuckets in ADR-0108 and --aihelp
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-712]
touches: [Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-918 — Record S3 DeleteObject, HeadBucket and ListBuckets in ADR-0108 and --aihelp

## Goal

ADR-0108 records, as Amendment 2, the decisions BL-712 took serving DeleteObject, HeadBucket and
ListBuckets, and `surl --aihelp s3` and the manual say the S3 server serves them.

## Context

- BL-712 built the three operations in `Surl.Extended.Protocol.S3.UnitLibrary` and wrote its
  decisions under its own `Notes` (in `Tasks/Done`), because `Documentation/Planning/Decisions` was
  held by BL-687 while it ran and `Application/Surl.Cli.UnitLibrary` by BL-548.
- `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` (the `s3` topic paragraph, and the
  `--list-directories` sentence naming "S3 ListObjectsV2") and `ManualText.cs` (the S3 paragraph near
  "PutObject needs --allow-uploads and ListObjectsV2") still say every request but GetObject,
  HeadObject, PutObject and ListObjectsV2 is `501 NotImplemented`.
- The library's `CLAUDE.md` already describes the new behaviour; copy from it and BL-712's Notes.

## Acceptance criteria

- [ ] ADR-0108 has "Amendment 2 - DeleteObject, HeadBucket and ListBuckets as BL-712 built them",
      marked "Decided by Claude under Stewart's delegation", stating each of BL-712's Notes decisions
      (recognition, `--list-directories` for ListBuckets, the body's order and `CreationDate`,
      HeadBucket's answers, DeleteObject's `204`/`403` rules and the index stamp).
- [ ] The `s3` `--aihelp` paragraph and the manual name DeleteObject (needs `--allow-uploads`),
      HeadBucket and ListBuckets (needs `--list-directories`), and the `--list-directories` sentence
      names S3 ListBuckets beside ListObjectsV2.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green
      (`CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` included).

## Notes

Filed by BL-712.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
