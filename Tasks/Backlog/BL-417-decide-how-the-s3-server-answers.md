---
id: BL-417
title: Decide how the S3 server answers
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-622]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/S3.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-417 — Decide how the S3 server answers

## Goal

An ADR decides how `surl s3://` answers Amazon S3 REST requests: the operations, addressing, how buckets map
onto the content store, SigV4 checking, error bodies, TLS and limits. All of it is taken from the S3 API
Reference. A server-side requirement matrix traces each decision to a test-to-be.

## Context

- Oracle and rules: BL-543's ADR. The specification is the Amazon S3 API Reference
  (https://docs.aws.amazon.com/AmazonS3/latest/API/Welcome.html): GetObject, HeadObject, PutObject,
  ListObjectsV2, DeleteObject, ListBuckets, the "Error responses" page and its error-code table, and the user
  guide's "Virtual hosting of buckets". SigV4 is "Authenticating Requests (AWS Signature Version 4)".
- Which requests a client sends. This tells us what to accept and is not a verdict: the port's client
  `Curl.Extended.Protocol.S3.UnitLibrary`, and its client-side matrices `Documentation/Conformance/Extended/S3.md`
  and `S3-Signing.md` in the port (read with `git -C Z:\repos\Curl show origin/master:<path>`). The port's
  `Curl.Conformance.Extended.UnitLibrary/S3/S3ReferenceEndpoint` is copyable code.
- What Surl already has:
  - the content store (`Surl.Content`);
  - the HTTP/1.1 serving shared by BL-407;
  - SigV4 checking for `curl --aws-sigv4` in `Surl.Authentication` (`AwsSigV4*`; ADR-0043, ADR-0045);
  - accounts and secure-by-default (ADR-0032);
  - `--allow-uploads` and `--allow-anonymous` (ADR-0006 section 2).
- BL-401's ADR decides whether buckets are top-level content-store directories.
- Shared stores (Stewart, 2026-11-04): BL-622's ADR decides that the S3 server serves the **same** `ContentStore`
  instance as HTTP, FTP, SFTP and SMB - bucket = top-level folder, object = file - and where object metadata and
  ETags live. Do not re-decide that here; decision 3 below applies BL-622's mapping and adds only what is
  S3-specific. Layer 2's named shares, `--bucket <name>=<path>` and per-account share access come from the
  configuration ADRs that follow BL-622; leave the bucket lookup as the seam BL-622 names. Logins read the one
  account set (ADR-0032; BL-622 decision 4). An object uploaded over FTP and read with GetObject must work.

Decide and record:

1. The operations served, and the one answer for any other operation (e.g. `NotImplemented`, 501, with the S3
   XML error body).
2. Path-style and virtual-hosted addressing, and how the listener knows its own host name for virtual hosting.
3. The bucket and key mapping onto the store, and key names the store cannot hold.
4. SigV4 in the `Authorization` header form, and whether presigned query strings are served. Required by
   default. Anonymous reads only under `--allow-anonymous`, and writes only under `--allow-uploads` with a
   SigV4 login. Each refusal's status and S3 error code (`AccessDenied`, `SignatureDoesNotMatch`,
   `InvalidAccessKeyId`, `RequestTimeTooSkewed`, `XAmzContentSHA256Mismatch`), and how this differs from the HTTP
   server's `401` with challenges.
5. Whether `s3` listens with TLS (S3 endpoints are HTTPS) or in plain text, and the option that changes it.
6. ListObjectsV2's paging (`max-keys`, `continuation-token`, `prefix`, `delimiter`, `CommonPrefixes`) and its
   XML namespace.
7. Each ADR-0006 limit, and the largest object accepted.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, with each wire choice citing the S3 API
      Reference page it comes from, and is listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/S3.md` exists in the format BL-543's ADR decision 9 sets. It
      lists every requirement of the pages above that binds a server, each "covered by" naming the task
      (BL-418, BL-419, BL-420) that will cover it, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the store is shared.
