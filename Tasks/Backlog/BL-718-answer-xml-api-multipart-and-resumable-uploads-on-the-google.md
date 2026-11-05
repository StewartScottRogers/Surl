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
completed:
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

- [ ] ADR-0110 gains a decision for both upload forms, marked "Decided by Claude under Stewart's delegation".
- [ ] Named tests show a three-part multipart upload assembled and read back with GET Object, a part out of order
      refused `400 InvalidPartOrder`, an unknown upload ID `404 NoSuchUpload`, and parts invisible to a listing
      before completion.
- [ ] Named tests show a resumable upload started, sent in two `PUT`s (the first answered `308` with `Range`), and
      read back; a session past `--max-filesize` refused `400 EntityTooLarge`.
- [ ] `GoogleCloudStorage.md`'s rows for both forms name this task's tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-05: Created.
