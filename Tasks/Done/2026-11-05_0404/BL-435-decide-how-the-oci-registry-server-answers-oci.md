---
id: BL-435
title: Decide how the OCI registry server answers oci
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-622]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/OciRegistry.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-435 — Decide how the OCI registry server answers oci

## Goal

An ADR decides how `surl oci://` answers as an OCI Distribution Specification v1.1.0 registry: pull, push,
content discovery and management endpoints, digests, media types, token authentication, error bodies, TLS,
storage and limits. A server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - OCI Distribution Specification v1.1.0 (https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md):
    the endpoint table end-1 to end-13, pull, push (monolithic POST+PUT, single POST with `?digest=`, chunked
    PATCH with `Content-Range`, and cross-repository mount), content discovery (tags list, referrers),
    content management (DELETE), and error codes;
  - the OCI Image Specification's media types and descriptor digests (sha256, sha512);
  - Docker's Token Authentication Specification (the `WWW-Authenticate: Bearer realm=…,service=…,scope=…`
    challenge and the token endpoint's JSON).
- Which requests a client sends (what to accept, not a verdict): the port's
  `Curl.Extended.Protocol.OciRegistry.UnitLibrary` and its matrix `OciRegistry.md`.
- The spec's own conformance suite (opencontainers/distribution-spec `conformance/`) lists the workflows a
  registry must pass. Use it as a checklist, not as code.
- Shared stores (Stewart, 2026-11-04): BL-622's ADR decides that the registry keeps repositories in the
  **same** `ContentStore` as the other file servers - repository = top-level folder, laid out inside it as
  BL-622 decides (planning: the OCI Image Layout's `blobs/<alg>/<hex>` and `index.json`) - instead of a private
  area under `.surl/`. Decision 2 below therefore applies BL-622's layout and says only what it leaves open.
  Token-endpoint credentials come from the one account set (BL-622 decision 4).

Decide and record:

1. Which endpoints are served (all of end-1 to end-13 unless one is refused with a reason), and the
   `Docker-Distribution-API-Version` header.
2. Storage: where blobs, manifests and tags live (under the content store's `.surl/` area, as the mail store
   does under `.surl/mail`), and whether pushed content survives a restart.
3. Digest checking on blob upload and manifest push, the accepted digest algorithms, and the error for a mismatch
   (`DIGEST_INVALID`).
4. Authentication: token authentication with surl as its own token endpoint, with credentials from the accounts
   file (Basic on the token request) and scopes per repository and action. Anonymous pull only under
   `--allow-anonymous`, and push only under `--allow-uploads`. The token format: opaque random tokens kept in
   memory, or signed.
5. Error bodies (`{"errors":[{"code":…,"message":…,"detail":…}]}`), and the status for each code the spec lists.
6. TLS: whether `oci` is TLS by default, and the option.
7. Each ADR-0006 limit, upload session lifetime, and the largest blob.

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its spec section, and is
      listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/OciRegistry.md` exists in the matrix format, each "covered by" naming
      BL-436, BL-437 or BL-438, or "n/a: <reason>".

## Notes

- 2026-11-05: Decided in ADR-0115 from the Distribution Specification v1.1.0 text (read 2026-11-05), the Image
  Format Specification and Docker's Token Authentication Specification. The port's client and matrix were read
  as background only.
- Decision 2 applies ADR-0100 decision 2.4 (the repository folder is an OCI Image Layout on the shared content
  store), not the Goal's older `.surl/` plan. Media types come from `index.json`'s descriptors, so the object
  metadata index only caches the digest check.
- Defaults taken: opaque in-memory tokens (300 s), not signed ones; `403 DENIED` with `insufficient_scope` for
  a token lacking the action (RFC 6750 3.1, matching BL-438's acceptance); upload sessions expire after 1 hour
  idle; manifests up to 4 MiB; deletes served under `--allow-uploads`; `--oci-plaintext` mirrors
  `--s3-plaintext`.
- The matrix's two TLS rows (`OCI:Surl-Tls-1`, `OCI:Surl-Tls-2`) name BL-439, which serves `oci` from `surl`
  with its TLS and option. The criterion listed only BL-436 to BL-438, but BL-439 is the task that owns TLS, as
  BL-421 owns it in the S3 matrix.
- No `Requirements.md` row: ADR-0108 to ADR-0114 added none, FR-056 and FR-058 cover the server, and the file is
  outside this task's `touches`.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the store is shared.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0115 decides how surl oci:// answers as an OCI Distribution v1.1.0 registry, and OciRegistry.md traces it
