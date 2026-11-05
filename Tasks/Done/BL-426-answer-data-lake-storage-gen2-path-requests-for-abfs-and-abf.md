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
completed: 2026-11-05
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

- [x] Named tests show:
      - a file created, appended in two parts, flushed, then read back;
      - a flush at the wrong position refused with `InvalidFlushPosition`;
      - a directory created and listed recursively with continuation;
      - a missing path gets `404 PathNotFound` in the JSON body;
      - an append without uploads allowed is refused.
- [x] Each JSON body in the tests is written from the REST API page's example.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Plan as built (2026-11-05), all in `Surl.Extended.Protocol.AzureStorage.UnitLibrary`:

- The listen scheme picks the endpoint: `az` keeps `AzureBlobRequestAnswerer`; `abfs` and `abfss` get the new
  `AzureDataLakeRequestAnswerer`. The login moved out of the Blob answerer into `AzureLoginJudge`, shared by
  both, so a Data Lake caller is named with its own scheme. `Schemes` is now `az`, `abfs`, `abfss`;
  `Surl.Composition` does not register the server yet (BL-427), so no `--aihelp` row changes here.
- `AzureReply` writes the Data Lake JSON error (`AzureDataLakeErrorBody`, `Utf8JsonWriter` with the relaxed
  encoder so `doesn't` stays readable) when made with `writesJsonErrors`. New codes in `AzureError`:
  `PathNotFound`, `FilesystemNotFound`, `InvalidFlushPosition`, `ResourceTypeMismatch`, `PathConflict`,
  `DirectoryNotEmpty`, `ContentLengthMustBeZero`, `MissingRequiredQueryParameter`, with the Data Lake pages'
  messages.
- Tests: `AzurePathUpdateTests` (the acceptance cases: create, two appends, flush, read back;
  `ServeAsync_FlushAtTheWrongPosition_Is400InvalidFlushPosition`;
  `ServeAsync_AppendWithoutUploadsAllowed_Is403AuthorizationPermissionMismatch`), `AzurePathListTests`
  (`ServeAsync_DirectoryCreatedAndListedRecursively_PagesWithAContinuation`),
  `AzureDataLakeRequestAnswererTests` (`ServeAsync_ReadOfAMissingPath_Is404PathNotFound`), `AzurePathCreateTests`,
  `AzureStagedPathTests`, `AzureDataLakeErrorBodyTests`: 100 new tests. Each JSON body is written from the
  pages' `PathList` and `DataLakeStorageError` definitions in ADR-0109 decision 5.4's member order.
- Measured: library 100% line, 100% branch, worst CRAP 10, no member over complexity 10.

Decisions taken (Claude, under Stewart's delegation):

- **Where appends are staged.** ADR-0109 decision 5.5 says `.surl/objects/uploads/<id>/`; the content store
  has no API to write there, and its `ContentUploadSession` - a hidden dot-file beside the target, started
  from the committed bytes, bounded by `--max-filesize`, renamed over the file on commit - gives exactly the
  invisibility and atomic flush the decision asks for. So each staged file is one session in
  `AzurePathStagingArea`. The ADR could not be edited here: BL-530 holds `Documentation/Planning/Decisions`.
  Filed BL-755 to record it there.
- **Appends and flushes are served one at a time across the server** (one gate in `AzurePathStagingArea`),
  because an upload session is not safe for concurrent use. Only authenticated writers reach it, and the
  head and idle timeouts bound how long one holds it.
- **An append before the file's committed end** is `400 InvalidQueryParameterValue` (`position`), because
  its bytes would overwrite committed ones inside the session; the flush check covers gaps and overlaps
  among staged ranges.
- **Flush positions**: a flush to `n` needs the staged ranges, in position order, to start at the committed
  length and run contiguously to at least `n`; with nothing staged it succeeds only at the file's length.
- **Path - Delete** checks `--allow-uploads` first (`403`), so a recursive delete never removes part of a
  tree before being refused; entries the store hides stay, and their directory answers `409 DirectoryNotEmpty`.
- **A directory's ETag** is `"0x<last-write ticks in hexadecimal>"`, the service's `0x…` form; Path - List
  writes ETags without quotes, as List Blobs writes `Etag`.
- **HEAD on a file** is served as Path - Read's head, since it is the same answer without the body; a
  directory's head (Path - Get Properties) stays BL-716's.

Left out and filed as BL-754: `retainUncommittedData=true` (refused `400`), `beginFrom`, `x-ms-properties`,
the conditional fields on the Data Lake writes, and the JSON body on a Data Lake connection refused past
the connection limit (still the XML `Error`).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Data Lake endpoint answers Path - Read, Create, Update (append, flush), Delete and List on abfs and abfss, with JSON errors
