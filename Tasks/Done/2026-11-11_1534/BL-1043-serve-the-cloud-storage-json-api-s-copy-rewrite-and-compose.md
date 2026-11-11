---
id: BL-1043
title: Serve the Cloud Storage JSON API's copy, rewrite and compose on the gs server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1041]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage-Json.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1043 — Serve the Cloud Storage JSON API's copy, rewrite and compose on the gs server

## Goal

The `gs` server answers `objects.copy`, `objects.rewrite` and `objects.compose` on the JSON paths.

## Context

- Decision: ADR-0148 decision 7 (copy, rewrite, compose), decisions 4, 5 and 8.
- Share access is judged for source (read) and destination (write); `rewrite` always finishes in one call.

## Acceptance criteria

- [x] `copyTo` stores a new generation with metageneration 1 and the source's stored fields unless the body names new metadata; across buckets works.
- [x] `rewriteTo` answers `storage#rewriteResponse` with `done` true, `totalBytesRewritten`, `objectSize` and `resource`; a sent `rewriteToken` is ignored.
- [x] `compose` concatenates 1 to 32 sources in order into one new generation; 33 is `400 invalid`, a missing source `404 notFound`.
- [x] `ifGenerationMatch=0` and the source-generation preconditions are honoured (`412 conditionNotMet`); a result past `--max-filesize` is `413`.
- [x] A source the caller may not read is `404`, a destination it may not write `403 forbidden`.
- [x] 100% line and branch coverage, complexity at most 10; build and fast tests green.

## Notes

- Decided by Claude under Stewart's delegation; recorded as ADR-0148 Amendment 3: judging order, body metadata replaces the source's, an `ifSource…` failure is `412` (never `304`), compose body shape and the `404`/`412`/`400` rows, `--max-filesize` checked on the sources' total before any byte moves.
- Shared writer `GcsJsonObjectAssembly` serves copy, rewrite and compose; `GcsJsonRoute` now keeps its per-operation parameter sets in a dictionary (a switch over the enum left an unreachable jump-table slot that coverage counted).
- Removed two rows of `GcsJsonRequestAnswererTests` that expected `501` for copyTo and compose, which are now served.
- Library at 100% line and branch coverage; no method above complexity 10. `gs` `--aihelp` text is BL-1044.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. copy, rewrite and compose served on the gs JSON paths; library at 100% coverage
