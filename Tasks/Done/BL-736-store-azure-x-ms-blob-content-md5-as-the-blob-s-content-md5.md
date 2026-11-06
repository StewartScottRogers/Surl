---
id: BL-736
title: Store Azure x-ms-blob-content-md5 as the blob's Content-MD5
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-425]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-736 — Store Azure x-ms-blob-content-md5 as the blob's Content-MD5

## Goal

A Put Blob with `x-ms-blob-content-md5` stores that value unchecked as the blob's `Content-MD5`, and Get Blob, Get Blob Properties and List Blobs answer it, as ADR-0109 decision 5.6 says.

## Context

- ADR-0109 decision 5.6: "`x-ms-blob-content-md5`, when sent, is stored as the blob's `Content-MD5` without being checked".
- `Surl.Content`'s `ObjectMetadata` and `ObjectProperties` have no Content-MD5 field, so BL-425 (which may not touch `Surl.Content`) ignores the header; the index file format (`ObjectMetadataIndexFormat`) needs the field too, read back compatibly from an index written without it.
- Found while delivering BL-425.

## Acceptance criteria

- [x] `ObjectMetadata` and `ObjectProperties` carry an optional Content-MD5 that the index stores and reloads, with a named `Surl.Content.UnitTests` test reading an index written before the field existed.
- [x] Named Azure tests show `x-ms-blob-content-md5` stored by Put Blob and answered as `Content-MD5` by Get Blob Properties and in List Blobs' `Content-MD5` element.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

- Delivered directly rather than through every /feature agent stage: a two-project change whose behaviour ADR-0109 decision 5.6 already decides.
- `ObjectMetadata` and `ObjectProperties` gain an optional `ContentMd5`. On `ObjectProperties` it is an `init` property outside the primary constructor, so S3, GCS and the tests that construct it positionally are untouched.
- Index format (decided by Claude under Stewart's delegation): the header becomes `SURL-OBJECT-INDEX-2` and each entry carries six header fields, `Content-MD5` the sixth; an index starting `SURL-OBJECT-INDEX-1` is still read, five fields and no `Content-MD5`, and is rewritten as version 2 at the next write. A version header was chosen over a trailing optional section because the format already refuses trailing bytes, so an old reader refuses a new index cleanly rather than misreading it. `Content-MD5` counts toward `MaxMetadataBytes` as the other header fields do.
- Azure: `x-ms-blob-content-md5` is stored as sent, unchecked, by Put Blob and, through the shared `StoreMetadataAsync`, by Put Block List. Get Blob, Get Blob Properties and List Blobs answer it, else the ETag-derived MD5 as before (`AzureBlobAnswer.StoredMd5(ObjectProperties)`). A rename and Path - Update `setProperties` keep it (the bytes are unchanged); a flush drops it (the bytes changed). Put Blob's own response `Content-MD5` stays the body's MD5.
- Tests: `ObjectMetadataIndexTests.LoadAsync_IndexWrittenBeforeContentMd5WasKept_ReadsItsFiveHeaderFieldsAndNoContentMd5` and `LoadAsync_IndexWrittenWithAContentMd5_ReadsItBack`, pinned bytes updated; `AzurePutBlobTests.ServeAsync_PutBlobWithBlobContentMd5_StoresItUncheckedAndGetBlobPropertiesAnswersIt`, `AzureListBlobsTests.ServeAsync_BlobPutWithBlobContentMd5_IsListedWithThatContentMd5`. Content 680 tests, Azure 498, both at 100% line and branch.
- ADR-0100 decision 2.3.1 still describes version 1; `Documentation/Planning/Decisions` is in BL-718's `touches` (Doing), so BL-968 is filed to amend it rather than widening this task.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Azure x-ms-blob-content-md5 is stored unchecked and answered as Content-MD5 by Get Blob, Get Blob Properties and List Blobs; the object index reads v1 and writes v2
