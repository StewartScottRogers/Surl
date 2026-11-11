---
id: BL-944
title: Answer the conditional fields on Azure Put Block List
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-735, BL-1035]
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

- 2026-11-11 (lane 6): Change written but not built - `AzurePutBlob.ConditionRefusalAsync` made internal and called from `AzurePutBlockList.CommitAsync` inside the staging lock, after the body is read and before the entries are resolved (so a refusal leaves the blob and the staged blocks alone; conditions decide before `InvalidBlockList`, as Put Blob checks them before its body). Five tests added to `AzureBlockUploadTests` (412 for the four conditions with the staged block committed afterwards, 409 for `If-None-Match: *` on an existing blob, committed on a missing one). Left uncommitted for the shift to stash: `dotnet` cannot resolve the pinned SDK 10.0.401 (BL-1035). Also update the library CLAUDE.md "Not yet" line once green.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so the change cannot be built or tested
