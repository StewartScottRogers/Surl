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
completed: 2026-11-11
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

- Amendment 4 copies BL-711's ten decisions; names checked against the code (`S3Login.ErrorOf`, `S3Error`, `S3Checksum`, `S3Crc32`, `IHttpChunkSignatureChain`). Decision 4.3's table gained the two rows with their exact messages from `S3Error`. Item 9 also notes it corrects decision 4.5's "CRC-32 and CRC-32C are written by hand". Docs only: no `.cs` or project file touched, so `verify` was not run.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0108 Amendment 4 records BL-711's ten decisions; decision 4.3 lists ConflictingCredentials and ExpiryOutOfRange; Amendment 2 point 5 notes the 400 InvalidRequest change
