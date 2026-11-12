---
id: BL-962
title: Record BL-711's presigned URL, aws-chunked and checksum decisions in ADR-0108
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-711, BL-1035]
touches: [Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-962 — Record BL-711's presigned URL, aws-chunked and checksum decisions in ADR-0108

## Goal

ADR-0108 has an "Amendment 4 - presigned URLs, `aws-chunked` and checksums, as BL-711 built them"
recording, marked "Decided by Claude under Stewart's delegation", every decision BL-711's Notes list.

## Context

- BL-711 could not edit `Documentation/Planning/Decisions` while BL-701 held the folder, as BL-419
  could not while BL-463 held it (BL-727 recorded that one, Amendment 2).
- The decisions, each with its why, are under "Decisions" in BL-711's Notes
  (`Tasks/Done/.../BL-711-accept-s3-presigned-urls-aws-chunked-payloads-and-checksum-h.md`). Copy them
  as the amendment's numbered list; the code is `Surl.Authentication`'s `AwsSigV4QueryString`,
  `AwsSigV4AuthenticationMethod.VerifyQueryString`, `AwsSigV4ChunkSignatureChain`, and the S3
  server's `S3ObjectBodyReader`, `S3AwsChunkedDecoder`, `S3ChecksumRequest`.
- Also correct decision 4.3's table: a presigned URL with an `Authorization` too, and `X-Amz-Expires`
  out of range, are `400 InvalidArgument`; and Amendment 2 decision 5 (a chunked body with a decoded
  length but no `STREAMING-` hash is now `400 InvalidRequest`, not `501`).

## Acceptance criteria

- [x] `ADR-0108-how-the-s3-server-answers-s3.md` has Amendment 4, dated and marked "Decided by Claude
      under Stewart's delegation", with one numbered item per decision in BL-711's Notes.
- [x] Decision 4.3's table lists `ConflictingCredentials` and `ExpiryOutOfRange` with their errors.
- [x] Amendment 2 decision 5 notes that BL-711 changed the chunked-without-`STREAMING-` answer to `400 InvalidRequest`.

## Notes

- 2026-11-11 (lane 6): the ADR amendment is already written on branch
  `factory/BL-962-lane-3-20261111-060243` (commit 9ca80aec). Lane 3's integration build failed, and
  lane 6's `dotnet build` fails too, because the .NET SDK 10.0.401 that `global.json` pins is not
  installed (BL-1035), not because of the docs change. Returned to Backlog behind BL-1035; once it is
  Done, cherry-pick that commit, build, run the fast tests and finish.

- 2026-11-12 (lane 2): Amendment 4 was already taken by BL-918 (DeleteObject), so the BL-711 amendment is Amendment 9, and decision 4.3, Amendment 2 point 5 cross-references it as such. SDK 10.0.404 now builds.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 3 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-962-lane-3-20261111-060243; start with git cherry-pick --no-commit factory/BL-962-lane-3-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: the pinned .NET SDK 10.0.401 is not installed, so dotnet build fails; the ADR amendment is ready on factory/BL-962-lane-3-20261111-060243
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. ADR-0108 Amendment 9 records BL-711's presigned URL, aws-chunked and checksum decisions
