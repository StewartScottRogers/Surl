---
id: BL-716
title: Answer Azure List Containers and the Data Lake rename, properties and filesystem requests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-426]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-716 — Answer Azure List Containers and the Data Lake rename, properties and filesystem requests

## Goal

The Azure Storage server answers List Containers and Get Container Properties on the Blob endpoint, and Path -
Get Properties (`HEAD`), Path - Create with `x-ms-rename-source`, Path - Update `action=setProperties`,
Filesystem - Get Properties and Filesystem - List on the Data Lake endpoint, as ADR-0109 decision 1 lists them.

## Context

- Decision: ADR-0109 decisions 1, 2 and 5. Rules: ADR-0100 decision 2.1 (containers and filesystems are the
  top-level folders, plus `--bucket`), ADR-0102 decision 2.3 (a share the caller may not read is missing).
- Specifications: "List Containers", "Get Container Properties", Data Lake "Path - Get Properties", "Path -
  Create" (rename), "Path - Update" (setProperties, `x-ms-properties`), "Filesystem - Get Properties",
  "Filesystem - List".
- Hadoop's ABFS driver renames and reads properties on every write; the port's client does neither.

## Acceptance criteria

- [ ] Named tests show List Containers' `EnumerationResults` written from the page's example, listing only
      the containers the caller may read, with `prefix`, `maxresults` and `marker`.
- [ ] Named tests show Path - Get Properties of a file and a directory (`x-ms-resource-type`), a rename of a
      file and of a directory through the content store, and a rename whose source is missing `404
      SourcePathNotFound`.
- [ ] Named tests show `setProperties` stored in the object metadata index and returned in `x-ms-properties`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-423 (ADR-0109).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
