---
id: BL-1045
title: Serve the Cloud Storage JSON API's buckets and object reads and deletes on the gs server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-719]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage-Json.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1045 — Serve the Cloud Storage JSON API's buckets and object reads and deletes on the gs server

## Goal

The `gs` server answers `buckets.list`, `buckets.get`, `objects.list`, `objects.get` (resource and `alt=media`) and `objects.delete` on the JSON paths, beside the XML API on one listener, with the JSON error body.

## Context

- Decision: ADR-0148 decisions 1, 2, 3, 4, 5, 6 and the limits of 8. Specification: the Cloud Storage JSON API reference pages named in its Context. The oracle is the specification, never the Curl port (ADR-0083).
- Path routing is decided after the request head and before authentication; JSON accepts Bearer only.
- Adds the metageneration to the object metadata index (ADR-0148 decision 3); XML reads report the recorded value.
- JSON is written with `Utf8JsonWriter` and read with `JsonDocument`: no reflection.
- Rows of `Documentation/Conformance/Extended/GoogleCloudStorage-Json.md` naming this task are filled with test names.

## Acceptance criteria

- [x] A request under `/storage/v1/`, `/upload/storage/v1/` or `/download/storage/v1/` is JSON, any other the unchanged XML API (test for each).
- [x] The four reads, `alt=media` (with `Range` and conditions) and delete answer ADR-0148 decision 2's successes with decision 3's resources, byte-pinned in tests.
- [x] Every refusal and error row of ADR-0148 decisions 4 and 5 in this task's scope is a test with its fixed JSON body, and no client byte is echoed.
- [x] Every other JSON request is `501 notImplemented`.
- [x] The metageneration is recorded and XML reads report it; an object PUT over XML reads back over JSON with equal generation, MD5 and CRC-32C.
- [x] The library and its tests keep 100% line and branch coverage, complexity at most 10; build and fast tests are green.

## Notes

- Plan and decisions (ADR-0148 stands; no new ADR needed): the JSON path is a separate `GcsJsonRequestAnswerer` the XML answerer hands a path-style request to before the login; the login judge takes `isJson` and refuses in JSON rows; resources use `Utf8JsonWriter`.
- touches widened (rule 3; no task in Doing names them): `Foundation/Surl.Content.UnitLibrary` and its tests, because the metageneration lives in `ObjectMetadataIndex`. The index format is now version 3 (`SURL-OBJECT-INDEX-3`, metageneration after generation); versions 1 and 2 are still read (metageneration 1).
- Choices: `buckets.list` accepts only `project` besides the common parameters (the ADR lists no paging for buckets); `includeTrailingDelimiter` is validated and changes nothing, since the walk lists files and no file name ends in the delimiter; a bucket's `timeCreated` and `updated` are the folder's last-write time (the store keeps no creation time); an object's `timeCreated` is its generation read as a time (the last-write time when the generation is no time); a chosen fault whose status has no row is `backendError` with that status and message `Backend Error.`.
- Not in scope here: a connection refused past a limit is still the XML `503 SlowDown` (the grammar is unknown before a request); the `409 conflict` and `403 quotaExceeded` rows are writes (BL-1041); the failed-login delay is the authentication session's, shared by both grammars.
- Measured: both libraries 100% line and branch (Measure-CodeQuality); the build's CA1502 gate is green.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. gs serves the Cloud Storage JSON API's bucket and object reads, alt=media and delete beside the XML API, with the JSON error body, Bearer-only logins and a recorded metageneration
