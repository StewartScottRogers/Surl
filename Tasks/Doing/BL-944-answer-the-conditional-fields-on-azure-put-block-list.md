---
id: BL-944
title: Answer the conditional fields on Azure Put Block List
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-735]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-944 — Answer the conditional fields on Azure Put Block List

## Goal

Azure Put Block List answers the conditional fields as ADR-0109 decision 1 says for a write, as Put Blob and Delete Blob do since BL-735: any failing condition is `412 ConditionNotMet`, and `If-None-Match: *` on an existing blob is `409 BlobAlreadyExists`.

## Context

- ADR-0109 decision 1 ("Conditional headers") and the REST API pages "Put Block List" and "Specifying conditional headers for Blob service operations".
- BL-735 added `AzureConditions.WriteRefusal(fields, existing)` and calls it from `AzurePutBlob` and `AzureBlobRequestAnswerer`'s Delete Blob; `AzurePutBlockList` does not call it yet.
- Found while delivering BL-735, whose acceptance criteria named only Put Blob and Delete Blob.

## Acceptance criteria

- [ ] Named tests in `AzureBlockUploadTests` show a Put Block List with a failing `If-Match`, `If-Unmodified-Since`, `If-None-Match` (a matching tag) and `If-Modified-Since` answered `412 ConditionNotMet`, the blob unchanged and its uncommitted blocks still staged.
- [ ] A named test shows `If-None-Match: *` on an existing blob answered `409 BlobAlreadyExists`, and on a missing blob committed.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
