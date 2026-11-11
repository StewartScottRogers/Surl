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
completed:
---
# BL-1041 — Serve the Cloud Storage JSON API's media and multipart uploads, patch and update on the gs server

## Goal

The `gs` server answers `objects.insert` with `uploadType=media` and `multipart`, `objects.patch` and `objects.update` on the JSON paths.

## Context

- Decision: ADR-0148 decision 7 (media, multipart, patch, update), decision 3 (metadata fields, metageneration), decisions 4, 5 and 8.
- `multipart/related` (RFC 2387) is read by hand with bounded buffers: two parts, JSON metadata then bytes.
- Writes need `--allow-uploads` and a login; hashes sent are checked.

## Acceptance criteria

- [ ] Media insert stores the object as PUT Object does, `name` required (`400 required` without).
- [ ] Multipart insert stores bytes and metadata; a third part, no boundary or an unterminated body is `400 badRequest`; metadata past 1 MiB is `400 invalid`.
- [ ] `patch` merges (a `null` removes) and `update` replaces writable metadata; both raise the metageneration and leave bytes and generation alone.
- [ ] `ifGenerationMatch=0` stores only when none exists; a failing condition is `412 conditionNotMet`; a body past `--max-filesize` is `413 uploadTooLarge`.
- [ ] A hash mismatch is `400 invalid` and stores nothing; a write with `--allow-uploads` off is `403 forbidden`.
- [ ] 100% line and branch coverage, complexity at most 10; build and fast tests green.

## Notes

## Log

- 2026-11-11: Created.
