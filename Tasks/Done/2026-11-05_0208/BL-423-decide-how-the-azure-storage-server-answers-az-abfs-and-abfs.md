---
id: BL-423
title: Decide how the Azure Storage server answers az, abfs and abfss
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-622]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/AzureStorage.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-423 — Decide how the Azure Storage server answers az, abfs and abfss

## Goal

An ADR decides how `surl az://` answers the Azure Blob service REST API, and how `surl abfs://` and `abfss://`
answer the Data Lake Storage Gen2 REST API, at `x-ms-version: 2025-01-05`: the operations, addressing,
authentication, error bodies, TLS and limits. A server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - Blob service REST API (https://learn.microsoft.com/rest/api/storageservices/blob-service-rest-api): Get Blob,
    Get Blob Properties, Put Blob, List Blobs, Delete Blob, "Status and error codes", and "Versioning for the
    Azure Storage services";
  - Data Lake Storage Gen2 REST API: Path - Read, Path - Create, Path - Update (append and flush), Path - List,
    Path - Delete, and the error JSON;
  - "Authorize with Microsoft Entra ID" (bearer tokens) and "Authorize with Shared Key".
- Which requests a client sends (what to accept, not a verdict): the port's
  `Curl.Extended.Protocol.AzureStorage.UnitLibrary` and its matrices `AzureStorage.md` and
  `AzureStorage-DataLake.md`. The port's reference endpoints `AzureBlobReferenceEndpoint` and
  `AzureDataLakeReferenceEndpoint` are copyable code.
- Surl has bearer checking (`BearerAuthenticationMethod` in `Surl.Authentication`) and accounts (ADR-0032), and
  the HTTP/1.1 serving from BL-407. BL-401's ADR decides the store mapping.
- Shared stores (Stewart, 2026-11-04): BL-622's ADR decides that the Azure Storage server serves the **same**
  `ContentStore` instance as HTTP, FTP, SFTP, SMB and S3 - container (and Data Lake filesystem) = top-level
  folder, blob or path = file - and where blob metadata and ETags live. Decision 2 below applies BL-622's
  mapping and adds only what is Azure-specific. Named shares, `--bucket` mapping and per-account access come from
  the configuration ADRs after BL-622. Shared Key and bearer credentials come from the one account set (BL-622
  decision 4). A file uploaded over FTP and read with Get Blob must work.

Decide and record:

1. The operations served on each endpoint, and the answer to any other.
2. How `<account>.blob.core.windows.net` and `<account>.dfs.core.windows.net` style hosts and path-style
   `/<account>/<container>` requests map onto the listener and the store.
3. Authentication: Entra bearer tokens checked how (against the accounts file's tokens), Shared Key and SAS
   (served or refused), and anonymous only under `--allow-anonymous`. Writes only under `--allow-uploads`.
4. The required headers (`x-ms-version`, `x-ms-date`) and the error for a missing one.
5. The error bodies: Blob XML `<Error><Code>` with `x-ms-error-code`, and the Data Lake JSON `{"error":{…}}`.
6. TLS: `abfss` over TLS. Whether `az` and `abfs` are plain text by default, and the options.
7. Each ADR-0006 limit, and the largest blob.

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each wire choice citing its REST API
      page, and is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/AzureStorage.md` exists in BL-543's ADR's matrix format, with every
      server-binding requirement of the pages above, each "covered by" naming BL-424, BL-425 or
      BL-426, or "n/a: <reason>".

## Notes

- ADR-0109 (next free number; ADR-0108 was the last). Pages read live on 2026-11-05: "Authorize with Microsoft
  Entra ID" (the Bearer challenge's exact 401 and `WWW-Authenticate`), "Common REST API error codes", "Blob
  Storage error codes", Data Lake "Path - Create", "Path - Update" and "Path - List" (codes, messages, the
  `DataLakeStorageError` shape, the Path schema's integer and boolean types).
- Main choices, each with its reason in the ADR: the listen scheme picks Blob or Data Lake; the account is never
  a namespace (one shared store), path-style `/<account>/…` only for IP-literal or `localhost` hosts; unserved
  requests get the pages' own `UnsupportedHttpVerb`, `InvalidQueryParameterValue` or `UnsupportedHeader` rather
  than Azurite's unlisted `501 NotImplemented`; leases, snapshots, ACLs, append and page blobs refused because
  other protocols would write past them; Bearer checked opaquely; Shared Key and service/account SAS served,
  user delegation SAS refused; `--az-plaintext` added beside `--s3-plaintext`.
- The Curl port's client uploads in blocks from 8 MiB, which BL-425 does not cover, so the ADR serves Put Block
  and Put Block List. Filed BL-717 (Shared Key and SAS; needs `Surl.Authentication` and Abstractions, after
  BL-420), BL-715 (blocks) and BL-716 (List Containers, Data Lake rename, properties and filesystem requests).
  The matrix's "covered by" therefore also names those three and BL-427 (TLS and the option), for rows BL-424 to
  BL-426 do not build; a row naming only BL-424 to BL-426 would have left them unowned.
- Added a Notes line to BL-427 for `--az-plaintext` and the plain-text bearer rule.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the store is shared.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0109 decides how az, abfs and abfss are answered; AzureStorage.md traces every server row to BL-424 to BL-427 or BL-717 to BL-716
