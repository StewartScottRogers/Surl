---
id: BL-429
title: Decide how the Google Cloud Storage server answers gs
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-429 — Decide how the Google Cloud Storage server answers gs

## Goal

An ADR decides how `surl gs://` answers the Cloud Storage XML API: the object operations, addressing, OAuth
bearer and V4 HMAC (`GOOG4-HMAC-SHA256`) authentication, error bodies, TLS and limits. A server-side requirement
matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - the Cloud Storage XML API (https://cloud.google.com/storage/docs/xml-api/overview): GET Object, HEAD Object,
    PUT Object, DELETE Object, List Objects (`ListBucketResult`), and "HTTP status and error codes";
  - the "V4 signing process" (https://cloud.google.com/storage/docs/authentication/signatures) and its
    canonical-requests page, in the `Authorization` header form.
- Which requests a client sends (what to accept, not a verdict): the port's
  `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` and its matrices `GoogleCloudStorage.md` and
  `GoogleCloudStorage-Signing.md`, including the `STORAGE_EMULATOR_HOST` endpoint override. The port's reference
  authorizer `GcsV4HmacReferenceAuthorizer` is copyable code.
- V4 HMAC is SigV4's scheme with Google's names (`GOOG4-HMAC-SHA256`, `x-goog-date`, `x-goog-content-sha256`,
  the `goog4_request` scope). `Surl.Authentication` already has `AwsSigV4*`; BL-430 adds the Google variant
  beside it.

Decide and record:

1. The operations served, and the answer to any other.
2. Path-style `storage.googleapis.com/<bucket>/<object>` and virtual-hosted `<bucket>.storage.googleapis.com`
   addressing on the listener, and the bucket mapping onto the store.
3. Authentication: bearer tokens checked against the accounts file, V4 HMAC with HMAC keys from the accounts
   file, anonymous only under `--allow-anonymous`, and writes only under `--allow-uploads`. The XML error code
   for each refusal (`AccessDenied`, `SignatureDoesNotMatch`, `InvalidArgument`).
4. TLS: whether `gs` is TLS by default, and the option.
5. Each ADR-0006 limit.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 5, each wire choice citing its XML API page,
      and is listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/GoogleCloudStorage.md` exists in the matrix format, each "covered by"
      naming BL-431, BL-430 or BL-432, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
