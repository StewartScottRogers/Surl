---
id: BL-709
title: Answer S3 DeleteObject, HeadBucket and ListBuckets
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-419]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-709 — Answer S3 DeleteObject, HeadBucket and ListBuckets

## Goal

The S3 server deletes an object (`204`, also for a missing key), answers `HEAD /<bucket>`, and lists the
buckets the caller may read as `ListAllMyBucketsResult`.

## Context

- Decision: ADR-0108 decisions 1 and 3; matrix rows `S3:DeleteObject-1`, `S3:HeadBucket-1`,
  `S3:ListBuckets-1` and `S3:ListBuckets-2` in `Documentation/Conformance/Extended/S3.md`.
- Specification: the S3 API Reference pages "DeleteObject", "HeadBucket" and "ListBuckets"; XML bodies written
  from each page's example, never from the Curl port.
- Buckets are top-level folders of the shared content store (ADR-0100 decision 2.1); a key ending in `/`
  removes its folder only when it is empty. Deleting needs `--allow-uploads` and a login (ADR-0108 decision 4.1).

## Acceptance criteria

- [ ] Named tests show DeleteObject removing a file and answering `204`, and `204` for a key that does not exist.
- [ ] Named tests show HeadBucket `200` for a bucket, `404` with no body for a missing one or one the caller may
      not read.
- [ ] A named test shows ListBuckets listing the top-level folders the caller may read, not top-level files or
      `.surl`, with the XML of the page's example.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-417 (ADR-0108), which serves these three operations beyond BL-418 and BL-419.

## Log

- 2026-11-05: Created.
