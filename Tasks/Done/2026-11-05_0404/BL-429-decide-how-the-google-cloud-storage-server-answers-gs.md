---
id: BL-429
title: Decide how the Google Cloud Storage server answers gs
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-622]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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
- Shared stores (Stewart, 2026-11-04): BL-622's ADR decides that the Cloud Storage server serves the **same**
  `ContentStore` instance as HTTP, FTP, SFTP, SMB, S3 and Azure - bucket = top-level folder, object = file -
  and where object metadata lives. Decision 2's "bucket mapping onto the store" applies BL-622's mapping. Named
  shares, `--bucket` mapping and per-account access come from the configuration ADRs after BL-622. HMAC keys and
  bearer tokens come from the one account set (BL-622 decision 4). A file uploaded over FTP and read with GET
  Object must work.

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

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 5, each wire choice citing its XML API page,
      and is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/GoogleCloudStorage.md` exists in the matrix format, each "covered by"
      naming BL-431, BL-430 or BL-432, or "n/a: <reason>".

## Notes

- 2026-11-05: ADR-0110 written from the XML API pages read that day (overview, GET Bucket, GET Object,
  "HTTP status and error codes", "HTTP headers and common request headers"); `cloud.google.com` now redirects to
  `docs.cloud.google.com`. Matrix: `Documentation/Conformance/Extended/GoogleCloudStorage.md`.
- Choices where the pages left one: `NotImplemented` is `400` (the page's status, unlike S3's 501); no credential
  is `401 AuthenticationRequired` with `WWW-Authenticate: Bearer realm="storage"` (RFC 9110 needs a challenge on
  401); no `Details` element (no client byte echoed); unmappable names `400`/`409 InvalidObjectName` (amends
  ADR-0100's JSON planning row); a full metadata index `403 InsufficientQuota`; body-hash mismatch `400 BadDigest`
  (the XML API has no `XAmzContentSHA256Mismatch`); both listing versions served; `AWS4-HMAC-SHA256` refused on
  `gs` (S3 clients have `s3://`).
- BL-431 gains DELETE Object, List Buckets, the version 2 listing and the `NotImplemented` answer (its Context and
  acceptance amended); BL-432 and BL-433 Context name ADR-0110's decisions. The TLS rows name BL-433 as "n/a" from
  the server library's matrix because the criterion lists only BL-430 to BL-432.
- Filed BL-718 (XML multipart and resumable uploads) and BL-719 (decide the JSON API), so neither is left out.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the store is shared.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0110 decides how surl gs:// answers the Cloud Storage XML API; GoogleCloudStorage.md traces each row to BL-430, BL-431, BL-432 or n/a
