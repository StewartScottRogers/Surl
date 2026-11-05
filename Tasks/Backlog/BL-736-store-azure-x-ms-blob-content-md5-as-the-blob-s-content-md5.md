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
completed:
---
# BL-736 — Store Azure x-ms-blob-content-md5 as the blob's Content-MD5

## Goal

A Put Blob with `x-ms-blob-content-md5` stores that value unchecked as the blob's `Content-MD5`, and Get Blob, Get Blob Properties and List Blobs answer it, as ADR-0109 decision 5.6 says.

## Context

- ADR-0109 decision 5.6: "`x-ms-blob-content-md5`, when sent, is stored as the blob's `Content-MD5` without being checked".
- `Surl.Content`'s `ObjectMetadata` and `ObjectProperties` have no Content-MD5 field, so BL-425 (which may not touch `Surl.Content`) ignores the header; the index file format (`ObjectMetadataIndexFormat`) needs the field too, read back compatibly from an index written without it.
- Found while delivering BL-425.

## Acceptance criteria

- [ ] `ObjectMetadata` and `ObjectProperties` carry an optional Content-MD5 that the index stores and reloads, with a named `Surl.Content.UnitTests` test reading an index written before the field existed.
- [ ] Named Azure tests show `x-ms-blob-content-md5` stored by Put Blob and answered as `Content-MD5` by Get Blob Properties and in List Blobs' `Content-MD5` element.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
