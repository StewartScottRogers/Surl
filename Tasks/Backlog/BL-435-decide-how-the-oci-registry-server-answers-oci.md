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
completed:
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

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its spec section, and is
      listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/OciRegistry.md` exists in the matrix format, each "covered by" naming
      BL-436, BL-437 or BL-438, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the store is shared.
