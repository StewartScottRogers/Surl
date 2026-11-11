---
id: BL-1045
title: Serve the Cloud Storage JSON API's buckets and object reads and deletes on the gs server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-719]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage-Json.md]
requirement: none
created: 2026-11-11
completed:
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

- [ ] A request under `/storage/v1/`, `/upload/storage/v1/` or `/download/storage/v1/` is JSON, any other the unchanged XML API (test for each).
- [ ] The four reads, `alt=media` (with `Range` and conditions) and delete answer ADR-0148 decision 2's successes with decision 3's resources, byte-pinned in tests.
- [ ] Every refusal and error row of ADR-0148 decisions 4 and 5 in this task's scope is a test with its fixed JSON body, and no client byte is echoed.
- [ ] Every other JSON request is `501 notImplemented`.
- [ ] The metageneration is recorded and XML reads report it; an object PUT over XML reads back over JSON with equal generation, MD5 and CRC-32C.
- [ ] The library and its tests keep 100% line and branch coverage, complexity at most 10; build and fast tests are green.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
