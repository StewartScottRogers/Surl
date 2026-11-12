---
id: BL-1041
title: Serve the Cloud Storage JSON API's media and multipart uploads, patch and update on the gs server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1045]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage-Json.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1041 — Serve the Cloud Storage JSON API's media and multipart uploads, patch and update on the gs server

## Goal

The `gs` server answers `objects.insert` with `uploadType=media` and `multipart`, `objects.patch` and `objects.update` on the JSON paths.

## Context

- Decision: ADR-0148 decision 7 (media, multipart, patch, update), decision 3 (metadata fields, metageneration), decisions 4, 5 and 8.
- `multipart/related` (RFC 2387) is read by hand with bounded buffers: two parts, JSON metadata then bytes.
- Writes need `--allow-uploads` and a login; hashes sent are checked.

## Acceptance criteria

- [x] Media insert stores the object as PUT Object does, `name` required (`400 required` without).
- [x] Multipart insert stores bytes and metadata; a third part, no boundary or an unterminated body is `400 badRequest`; metadata past 1 MiB is `400 invalid`.
- [x] `patch` merges (a `null` removes) and `update` replaces writable metadata; both raise the metageneration and leave bytes and generation alone.
- [x] `ifGenerationMatch=0` stores only when none exists; a failing condition is `412 conditionNotMet`; a body past `--max-filesize` is `413 uploadTooLarge`.
- [x] A hash mismatch is `400 invalid` and stores nothing; a write with `--allow-uploads` off is `403 forbidden`.
- [x] 100% line and branch coverage, complexity at most 10; build and fast tests green.

## Notes

- Delivered by one run of /task-run (feature pipeline) in `Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` and its
  `.UnitTests`, plus the matrix `GoogleCloudStorage-Json.md`. New types: `GcsJsonObjectInsert` (media and multipart),
  `GcsJsonObjectUpdate` (patch and update), `GcsJsonObjectFields` (the JSON metadata body), `GcsMultipartRelatedSink` and
  `GcsMultipartRelatedProblem` (the by-hand `multipart/related` reader, bounded buffers). `GcsJsonRoute` now recognises
  `POST` on the upload path and `PATCH`/`PUT` on an object; `GcsPutObject` gained an overload that stores a built
  `ObjectMetadata`; `GcsJsonConditions.AreMetByWrite` judges a write against a maybe-absent object.
- Decisions taken (ADR-0148 Amendment 1, "Decided by Claude under Stewart's delegation"): names ending in `/` are
  `400 invalid`; digests come from the head and, for multipart, the metadata; resumable stays `501` until BL-1042; patch and
  update of a share file are `403 forbidden`; a refused commit is `403 forbidden`; `quotaExceeded`, `badRequest`,
  `conflict`, `lengthRequired` and `uploadTooLarge` rows added.
- Four existing tests asserted `501` for requests now served (PUT/PATCH of an object, a POST to the upload path); they were
  changed to unlisted requests that are still `501`.
- Coverage of the library: 100% line, 100% branch (Measure-CodeQuality). CA1502 passes at build (at most 10). The
  Cobertura-complexity column lists members above 10 that were already there; mine are no worse than they are.
- `GcsServerHarness` takes an optional `storeFaults` plan, to reach the commit refusal of an out-of-space fault.
- Unrelated: `Surl.Extended.Protocol.WebTransport.UnitTests` once failed
  `ServeStreamAsync_StreamWhoseConnectWasRefused_IsResetWithBufferedStreamRejected` in a solution-wide run and passed on
  the rerun; not touched here.
## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. The gs server answers Cloud Storage JSON media and multipart inserts, patch and update; 100% coverage, build and fast tests green.
