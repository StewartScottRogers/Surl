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
completed:
---
# BL-735 — Answer the conditional fields on Azure Put Blob and Delete Blob

## Goal

Azure Put Blob and Delete Blob answer the conditional fields as ADR-0109 decision 1 says: any failing condition on a write is `412 ConditionNotMet`, and `If-None-Match: *` on an existing blob is `409 BlobAlreadyExists`.

## Context

- ADR-0109 decision 1 ("Conditional headers") and the REST API page "Specifying conditional headers for Blob service operations".
- BL-425 serves Put Blob (`AzurePutBlob`) and Delete Blob (`AzureBlobRequestAnswerer.DeleteBlob`) without reading `If-Match`, `If-None-Match`, `If-Modified-Since` or `If-Unmodified-Since`; reads already use `AzureConditions`.
- Found while delivering BL-425; its acceptance criteria did not name them.

## Acceptance criteria

- [ ] Named tests show a Put Blob and a Delete Blob with a failing `If-Match`, `If-Unmodified-Since`, `If-None-Match` (a matching tag) and `If-Modified-Since` answered `412 ConditionNotMet` with nothing changed.
- [ ] A named test shows `If-None-Match: *` on an existing blob answered `409 BlobAlreadyExists`, and on a missing blob stored.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
