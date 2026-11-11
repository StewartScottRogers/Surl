---
id: BL-719
title: Decide how the Google Cloud Storage server answers the JSON API
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-429]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/GoogleCloudStorage-Json.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-719 — Decide how the Google Cloud Storage server answers the JSON API

## Goal

An ADR decides whether and how the `gs` server also answers the Cloud Storage JSON API
(`/storage/v1/b/<bucket>/o…`, `/upload/storage/v1/…`, `/download/storage/v1/…`) on the same listener and store,
and files the build tasks.

## Context

- ADR-0110 (BL-429) serves the XML API only and left the JSON API to this task ("Alternatives considered").
  ADR-0100 decision 2.2's planning row first named JSON error reasons (`invalid`, `conflict`) for GCS.
- Google's client libraries and `gcloud storage` speak the JSON API to an emulator named by
  `STORAGE_EMULATOR_HOST`; the port's client speaks the XML API. Background only, never a verdict (ADR-0083).
- Specification: Cloud Storage JSON API reference ("Objects: get, insert, list, delete", "Buckets: list",
  "HTTP status and error codes" for the JSON `error` body), and the upload pages (`uploadType=media`,
  `multipart`, `resumable`).

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation", deciding the JSON operations served, their paths beside the XML API's
      on one listener, the JSON error body, authentication (the same Bearer and accounts as ADR-0110), and the
      limits; it is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/GoogleCloudStorage-Json.md` exists in the matrix format, each row's
      "covered by" naming a task filed by this one or "n/a: <reason>".

## Notes

- Decided ADR-0148 (Decided by Claude under Stewart's delegation): JSON served on the same listener, told apart by path; buckets not created or deleted (revisit with s3/az); Bearer only; metageneration added to the index. Filed BL-1040 to BL-1044 (one library, so serial; BL-1044 waits on BL-1026).
- Docs-only; no .cs touched, so no build needed.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0148 decides the gs JSON API; matrix GoogleCloudStorage-Json.md written; BL-1040 to BL-1044 filed
