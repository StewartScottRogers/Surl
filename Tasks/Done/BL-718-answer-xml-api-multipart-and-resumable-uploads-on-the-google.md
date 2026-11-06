---
id: BL-718
title: Answer XML API multipart and resumable uploads on the Google Cloud Storage server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation/Planning/Decisions, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-718 — Answer XML API multipart and resumable uploads on the Google Cloud Storage server

## Goal

The `gs` server answers the XML API multipart upload (`POST ?uploads`, `PUT ?partNumber&uploadId`,
`POST ?uploadId`, `DELETE ?uploadId`, `GET ?uploadId`) and resumable upload (`POST` with
`x-goog-resumable: start`, then `PUT` to the session URI with `Content-Range`) requests, which ADR-0110
decision 1 answers `400 NotImplemented` for now.

## Context

- ADR-0110 (BL-429), "Alternatives considered": both were left to this task. ADR-0108 decisions 1 and 7
  (S3's multipart uploads, BL-713) are the model: staging under ADR-0100's `.surl/objects/uploads/`,
  parts invisible until completion, 1000 open uploads, 24-hour idle abort on `TimeProvider`.
- Specifications: Cloud Storage XML API "XML API multipart uploads" and "Resumable uploads" pages; codes from
  "HTTP status and error codes" (`InvalidPart`, `InvalidPartOrder`, `NoSuchUpload`, `EntityTooSmall`).
- Record the wire choices (session URI form, `308 Resume Incomplete` with `Range`, limits) as an amendment to
  ADR-0110 in the same diff, and add matrix rows to `GoogleCloudStorage.md`.

## Acceptance criteria

- [x] ADR-0110 gains a decision for both upload forms, marked "Decided by Claude under Stewart's delegation".
- [x] Named tests show a three-part multipart upload assembled and read back with GET Object, a part out of order
      refused `400 InvalidPartOrder`, an unknown upload ID `404 NoSuchUpload`, and parts invisible to a listing
      before completion.
- [x] Named tests show a resumable upload started, sent in two `PUT`s (the first answered `308` with `Range`), and
      read back; a session past `--max-filesize` refused `400 EntityTooLarge`.
- [x] `GoogleCloudStorage.md`'s rows for both forms name this task's tests.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Decided in ADR-0110 amendment 2 (decision 9, and decision 7 amended), from the XML API pages read on
  2026-11-06. Choices with a sensible default, and why:
  - Staging is one uncommitted content-store upload session per upload at the object name (a hidden
    dot-file), as BL-713 did for S3, not ADR-0100's `.surl/objects/uploads/` folder: the session is
    already hidden, discardable and bounded by `--max-filesize`.
  - Session URI `<scheme>://<Host><path>?upload_id=<id>` (Google's own query name); `201` on start, `308
    Resume Incomplete` with `Range: bytes=0-<n>` (none when nothing is kept), `204` on cancel (the XML tab;
    the JSON API's `499` rejected). A later request to a cancelled session is `404 NoSuchUpload`, not the
    page's `204` again, to avoid remembering every cancelled ID.
  - A chunk not starting at the next byte is not read and answered `308` with the true `Range`; any chunk
    size is kept whole (no 256 KiB rounding).
  - `EntityTooSmall` (status page) rather than the completion page's `InvalidArgument` for a small part;
    a malformed completion body is `InvalidArgument`. Multipart ETag `"<md5 of md5s>-<n>"`, as the page's
    example `"…-9"`.
  - Limits: 1000 open uploads (both forms together), 24-hour idle discard (ADR-0108's), parts 1-10000,
    `max-parts` capped at 1000, completion body 1 MiB.
- Measured: library at 100% line and 100% branch coverage (cobertura from the MSTest collector), 335
  tests in `Surl.Extended.Protocol.GoogleCloudStorage.UnitTests`.
- `GcsPutObject` now shares its checks, folder creation, metadata storage and `Stored` answer with the
  uploads (`GcsUploadStaging`); `GcsOperationRecognition` matches the exact set of query names, so `GET
  ?` (an empty query) still reads, and every other shape stays `400 NotImplemented`.
- As in S3, the server-wide upload gate is held while a part or chunk body is read, so one slow upload
  request delays other upload requests (never plain reads or PUT Object). Acceptable for a test stand-in.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. gs answers XML API multipart uploads (initiate, parts, list parts, complete, abort) and resumable uploads (201 session URI, 308 with Range, 200, cancel), staged invisibly; ADR-0110 amendment 2
