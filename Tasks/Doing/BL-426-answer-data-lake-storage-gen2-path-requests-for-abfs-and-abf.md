---
id: BL-426
title: Answer Data Lake Storage Gen2 path requests for abfs and abfss
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-425]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-426 — Answer Data Lake Storage Gen2 path requests for abfs and abfss

## Goal

The Azure Storage server answers the Data Lake Storage Gen2 endpoint that `abfs` and `abfss` use: Path - Read,
Path - Create (file and directory), Path - Update (`append` then `flush`), Path - List (with `recursive` and
`continuation`) and, if the ADR serves it, Path - Delete. Errors are returned as the Gen2 JSON error body.

## Context

- Decision: BL-423's ADR, decisions 1, 2 and 5.
- Data Lake Storage Gen2 REST API: Path - Create (`?resource=file|directory`), Path - Update
  (`?action=append&position=N` then `?action=flush&position=M`; a position that does not match is refused with
  `InvalidFlushPosition`), Path - List (`?resource=filesystem&recursive=…&directory=…`, the `{"paths":[…]}` JSON,
  `x-ms-continuation`), Path - Read, and the error JSON `{"error":{"code":…,"message":…}}`.
- Uploads only under `--allow-uploads`, as Put Blob.

## Acceptance criteria

- [ ] Named tests show:
      - a file created, appended in two parts, flushed, then read back;
      - a flush at the wrong position refused with `InvalidFlushPosition`;
      - a directory created and listed recursively with continuation;
      - a missing path gets `404 PathNotFound` in the JSON body;
      - an append without uploads allowed is refused.
- [ ] Each JSON body in the tests is written from the REST API page's example.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
