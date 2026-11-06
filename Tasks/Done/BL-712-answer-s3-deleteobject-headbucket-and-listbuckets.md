---
id: BL-712
title: Answer S3 DeleteObject, HeadBucket and ListBuckets
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-419]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-712 — Answer S3 DeleteObject, HeadBucket and ListBuckets

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

- [x] Named tests show DeleteObject removing a file and answering `204`, and `204` for a key that does not exist.
- [x] Named tests show HeadBucket `200` for a bucket, `404` with no body for a missing one or one the caller may
      not read.
- [x] A named test shows ListBuckets listing the top-level folders the caller may read, not top-level files or
      `.surl`, with the XML of the page's example.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-417 (ADR-0108), which serves these three operations beyond BL-418 and BL-419.

Built (2026-11-06): `S3OperationRecognition` recognises the three; `S3DeleteObject`, `S3ListBuckets`
and `S3XmlElement` (the escaping `S3ListObjectsV2` already used, now shared) are new; `S3RequestAnswerer`
answers ListBuckets before the bucket check and HeadBucket as `200` with no body.

Decisions (Decided by Claude under Stewart's delegation; to be written into ADR-0108 as Amendment 2 by
BL-918, because `Documentation/Planning/Decisions` was held by BL-687 while this ran):

1. ListBuckets is `GET /` path style with no query; any query (`prefix`, `max-buckets`,
   `continuation-token`, `bucket-region`) and any other method on `/` is `501 NotImplemented` - an
   unfiltered list answering a filtered request would be untrue. HeadBucket is `HEAD /<bucket>` with no
   query; DeleteObject is `DELETE /<bucket>/<key>` with no query (`?versionId` stays `501`).
2. ListBuckets needs `--list-directories` (off: `403 AccessDenied`), as ListObjectsV2 does: it is a
   listing in ADR-0006's sense, and secure by default. Buckets: every name the root listing gives the
   caller (shares included) plus every `--bucket`, kept when it maps for the caller to a folder (a
   top-level file, `.surl`, an unreadable share and a `--bucket` whose folder is missing are left out),
   once each, in UTF-8 binary order.
3. Body: the page's example order - `Buckets` with each `Bucket`'s `CreationDate` then `Name`, then
   `Owner` with `DisplayName` then `ID` (the signed account, or `anonymous`, as `fetch-owner` names it),
   with the 2006-03-01 namespace `ListBucketResult` uses. `CreationDate` is the folder's last-write
   time in `LastModified`'s form: the store keeps no folder creation time.
4. HeadBucket: `200`, common fields, no body; a missing bucket, a top-level file or a share the caller
   may not read is `404` with no body (the existing `HEAD` error rule).
5. DeleteObject: `--allow-uploads` first (off: `403 AccessDenied`), an anonymous `DELETE` refused by
   `S3Login` as every unsigned write is, then the bucket, then `KeyTooLongError`. A file key goes to
   `DeleteFile`; a key ending in `/` to `RemoveEmptyDirectory`, so a non-empty folder stays. Every
   outcome but `NotPermitted` is `204` (no entry, a folder without the `/`, an unmappable key, a
   non-empty folder); `NotPermitted` (readonly or read-only share) is `403 AccessDenied`. The object
   metadata index needs no removal: the entry's stamp no longer matches a file, so it reads as absent.

The `--aihelp` `s3` paragraph and the manual in `Surl.Cli` (held by BL-548) still list only the older
operations; BL-918 updates them with the ADR.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. S3 answers DeleteObject (204), HeadBucket (200/404, no body) and ListBuckets (ListAllMyBucketsResult of the readable top-level folders and --buckets)
