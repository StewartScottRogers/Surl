---
id: BL-686
title: Name media types from file extensions in Surl.Content for HTTP and the object-storage servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] A named test shows `Surl.Content` naming the media type for each extension in the ADR's table, and
      `application/octet-stream` for an unknown or missing extension.
- [ ] A named test shows the HTTP server's `Content-Type` and `ObjectMetadataIndex`'s default both use it.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green, with 100% line and branch coverage kept.

## Notes

## Log

- 2026-11-04: Created.
