---
id: BL-735
title: Answer the conditional fields on Azure Put Blob and Delete Blob
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-425]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-735 — Answer the conditional fields on Azure Put Blob and Delete Blob

## Goal

Azure Put Blob and Delete Blob answer the conditional fields as ADR-0109 decision 1 says: any failing condition on a write is `412 ConditionNotMet`, and `If-None-Match: *` on an existing blob is `409 BlobAlreadyExists`.

## Context

- ADR-0109 decision 1 ("Conditional headers") and the REST API page "Specifying conditional headers for Blob service operations".
- BL-425 serves Put Blob (`AzurePutBlob`) and Delete Blob (`AzureBlobRequestAnswerer.DeleteBlob`) without reading `If-Match`, `If-None-Match`, `If-Modified-Since` or `If-Unmodified-Since`; reads already use `AzureConditions`.
- Found while delivering BL-425; its acceptance criteria did not name them.

## Acceptance criteria

- [x] Named tests show a Put Blob and a Delete Blob with a failing `If-Match`, `If-Unmodified-Since`, `If-None-Match` (a matching tag) and `If-Modified-Since` answered `412 ConditionNotMet` with nothing changed.
- [x] A named test shows `If-None-Match: *` on an existing blob answered `409 BlobAlreadyExists`, and on a missing blob stored.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Delivered directly rather than through the full `/feature` agent stages: the change is one
  evaluator method and two call sites in one library, and ADR-0109 decision 1 already decides the
  behaviour, so no plan or new ADR was needed.
- `AzureConditions.WriteRefusal(fields, existing)` evaluates a write's conditions. Unlike a read,
  every condition counts and none decides over another (the ADR's "any failing condition"), so
  `If-Match` matching does not rescue a failing `If-Unmodified-Since`. `If-None-Match` naming `*`
  (alone or in a list) on an existing blob is `409 BlobAlreadyExists` and is checked first. A date
  that does not parse is ignored, as on reads.
- Default taken: with no blob at the name, only `If-Match` fails (`412`), since there is no tag to
  match; the date conditions and `If-None-Match` hold. This follows RFC 9110 §13.1.1 (`If-Match`
  is false when there is no current representation); the REST page was not checked on this case.
- Default taken: Delete Blob on a missing blob stays `404 BlobNotFound` whatever its conditions,
  because existence is checked first, as before. With uploads off, Delete Blob checks conditions
  before the store refuses the delete, so a failing condition there is `412` rather than `403`.
  Reading a blob's ETag is no more than Get Blob Properties already allows. Put Blob keeps
  `403` first, since it checks uploads before anything else.
- Put Blob checks the conditions once the name maps, before folders are created or any byte of
  the body is read, so a refused write leaves nothing behind.
- The existing blob's ETag and last-write time come from `AzureBlobPropertiesReader`, the same
  source as Get Blob, so a condition sees what a read would have answered.
- Measured: `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.AzureStorage.UnitLibrary`
  gives 100% line and 100% branch coverage. The 9 members it lists over complexity 10 (counted
  with async state machines) were all over it before this change; none is new. The build's
  CA1502 gate passes.
- Follow-up filed: BL-944, the same conditional fields on Put Block List.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Azure Put Blob and Delete Blob answer If-Match, If-None-Match, If-Modified-Since and If-Unmodified-Since: 412 ConditionNotMet, or 409 BlobAlreadyExists for If-None-Match: * on an existing blob
