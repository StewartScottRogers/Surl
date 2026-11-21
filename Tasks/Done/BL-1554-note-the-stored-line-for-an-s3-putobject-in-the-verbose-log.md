---
id: BL-1554
title: Note the stored line for an S3 PutObject in the verbose log
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1554 — Note the stored line for an S3 PutObject in the verbose log

## Goal

With `-v`, a PutObject the S3 server stores writes one exchange-log note in the HTTP server's form,
`PUT <target>: 200, <n> bytes stored at <location>`, so an operator or test reading the log sees where an S3
upload landed as it does for an HTTP `PUT`.

## Context

- Surly cookbook bug report 2026-11-21, item 5: `surl -v ... s3://127.0.0.1:PORT/` and an S3 put log no
  stored line, while `surl -v --allow-uploads http://...` with `curl -T f http://.../f` logs
  `PUT /f: 201, N bytes stored at ...`.
- The HTTP server's note: `HttpRequestAnswerer.CommittedUploadAnswer`
  (`Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.Uploads.cs:122-125`):
  `$"{request.Method} {request.Target}: {status.Code}, {storedLength} bytes stored at {target.Mapping.Location}"`,
  with the length from the committed `ContentUploadSession.Length`.
- The S3 server writes no such note: `S3PutObject.WriteAsync`
  (`Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/S3PutObject.cs:205-226`) commits the session
  (`:218`) and answers `Stored(reply, ...)` (`:225`) with nothing logged. `S3PutObject` already holds an
  `IExchangeLog log` (constructor, `:25`), and `mapping` (`ContentPathMapping.Location`) and `session.Length`
  are in scope there. No ADR decides S3's verbose notes; ADR-0108 is silent, so this follows the HTTP server's
  existing form rather than inventing one. The note is written only after the metadata index is stored, so a
  refused request never logs "stored".
- Out of scope: the zero-byte folder object (`StoreFolderObjectAsync`, `:290-306`), UploadPart and
  CompleteMultipartUpload; the Cloud Storage and Azure servers.
- Tests: `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests/S3PutObjectTests.cs`, reading
  `harness.Log.Notes` as the index-failure tests at `:452` and `:464` do.
- Stand-in: no store, event or fault changes; the `StoreChangedEvent` the store publishes is unchanged.

## Acceptance criteria

- [x] A stored PutObject of 5 bytes to `/bucket/x` notes exactly `PUT /bucket/x: 200, 5 bytes stored at <location>`,
  `<location>` the mapping's `Location`, asserted in `S3PutObjectTests`.
- [x] A PutObject refused for uploads off, a bad `Content-MD5` or a failed index write notes no stored line,
  each asserted.
- [x] A PutObject sent `aws-chunked` notes the decoded length, not the wire length.
- [x] `dotnet build Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary -warnaserror` is clean and
  `dotnet test Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests --filter "TestCategory!=Integration"`
  passes with `S3PutObject` at 100% line and branch coverage.
- [x] `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/CLAUDE.md` names the note.

## Notes

- Decision: "a failed index write" is read as the index refusing the object (index full, `503`), which
  removes the object. A storage failure rewriting the index file still leaves the object stored and
  answered `200`, so the stored line is true there and is written.
- The note is written in `S3PutObject.WriteAsync` after the metadata step succeeds.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. S3 PutObject notes 'PUT <target>: 200, <n> bytes stored at <location>' in the verbose log
