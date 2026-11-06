---
id: BL-962
title: Record BL-711's presigned URL, aws-chunked and checksum decisions in ADR-0108
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-711]
touches: [Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md]
requirement: none
created: 2026-11-06
completed:
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

- [ ] `ADR-0108-how-the-s3-server-answers-s3.md` has Amendment 4, dated and marked "Decided by Claude
      under Stewart's delegation", with one numbered item per decision in BL-711's Notes.
- [ ] Decision 4.3's table lists `ConflictingCredentials` and `ExpiryOutOfRange` with their errors.
- [ ] Amendment 2 decision 5 notes that BL-711 changed the chunked-without-`STREAMING-` answer to `400 InvalidRequest`.

## Notes

## Log

- 2026-11-06: Created.
