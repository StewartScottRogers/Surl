---
id: BL-686
title: Name media types from file extensions in Surl.Content for HTTP and the object-storage servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation/Planning/Decisions/ADR-0142-how-surl-names-a-files-media-type-from-its-extension.md, Documentation/Planning/Decisions/ADR-0008-how-the-http-server-answers-get-head-and-refusals.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-686 — Name media types from file extensions in Surl.Content for HTTP and the object-storage servers

## Goal

`Surl.Content` names a file's media type from its extension (else `application/octet-stream`), the HTTP server
answers `Content-Type` with it, and `ObjectMetadataIndex` answers it for a file stored with no `Content-Type`,
as ADR-0100 decision 2.3 says ("named by Surl.Content's media types from the extension, as HTTP names it").

## Context

- Today no media-type table exists: `HttpProtocolServer` answers every file `application/octet-stream` "until
  the content store knows media types", and `ObjectMetadataIndex.DefaultContentType` does the same (BL-636).
- Decide the table (ADR-0008 governs HTTP's answers) in an ADR; measure what upstream curl does with the answer
  (`--write-out %{content_type}`) with `Record-CurlExchange.ps1` before pinning bytes.

## Acceptance criteria

- [x] A named test shows `Surl.Content` naming the media type for each extension in the ADR's table, and
      `application/octet-stream` for an unknown or missing extension.
- [x] A named test shows the HTTP server's `Content-Type` and `ObjectMetadataIndex`'s default both use it.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green, with 100% line and branch coverage kept.

## Notes

- Decided in ADR-0142: `ContentMediaTypes.Of(path)` in `Surl.Content`, a fixed 36-extension table (same on
  every platform, no system MIME database), last segment's last `.`, any case, dot-files unknown, no
  `charset` (the store does not know a file's encoding), else `application/octet-stream`.
- Measured first: pinned curl 8.21.0 with `-w %{content_type}` reports the field verbatim and exits 0
  (`text/plain; charset=utf-8`, `image/png`). The eight HTTP fixtures that answer `file.txt` were
  re-recorded with `Content-Type: text/plain` by `Record-CurlExchange.ps1`; all exited 0.
- Tests: `ContentMediaTypesTests` (every table row, unknown/missing cases, path handling),
  `ObjectMetadataIndexTests.ReadAsync_FileWithNoStoredContentType_AnswersTheMediaTypeContentMediaTypesNamesFromTheKey`,
  `HttpRequestAnswererTests.AnswerAsync_GetOfAFile_NamesTheContentTypeContentMediaTypesNamesFromItsExtension`.
- Touches widened (no Doing task names them): the object-storage test projects, whose `.txt` expectations
  follow the index default; the S3, Azure and Cloud Storage libraries, whose share-file fallback used
  `DefaultContentType` directly - switched to the same table so a file answers one type on every server;
  ADR-0008 and the ADR index.
- `AiHelpProse` (Surl.Cli) still says HTTP answers `application/octet-stream`; Surl.Cli is held by BL-560,
  so BL-926 was filed to correct it.
- `dotnet format` reports ENDOFLINE across the whole checkout (LF working tree, pre-existing); no other
  format issue in the changed files.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Surl.Content names media types from extensions (ADR-0142); HTTP Content-Type and the object servers' defaults use it
