# Surl.Extended.Protocol.AzureStorage.UnitLibrary

The Azure Storage server (ADR-0109): answers the Azure Blob service REST API over HTTP/1.1 on the
shared `ContentStore`, whose top-level folders are the containers (ADR-0100 decision 2). An
extended server (ADR-0083): judged by the Azure Storage REST API reference at
`x-ms-version: 2025-01-05`, never by upstream curl (which has no `az`) and never by the Curl port.
Namespace `Surl.Extended.Protocol.AzureStorage`.

What it serves now (BL-424), on the `az` scheme only:

- Get Blob and Get Blob Properties on `/<container>/<blob>` (account-host form): one `Range` or
  `x-ms-range` (`x-ms-range` wins; `206`, `416 InvalidRange`, several ranges ignored),
  `x-ms-range-get-content-md5` (`400 OutOfRangeInput` past 4 MiB), the conditional fields
  (`412 ConditionNotMet`, `304`), and the blob's stored header fields and `x-ms-meta-*` from
  `ObjectMetadataIndex` (the index's defaults, computed here, for a file the index does not key).
  `Content-MD5` is the base64 of the ETag when the ETag is the index's quoted hexadecimal MD5.
- The required headers: a malformed `x-ms-version` or `x-ms-date` is `400 InvalidHeaderValue`; a
  request with `Authorization` and no `x-ms-version` is `400 MissingRequiredHeader`.
- The bearer login through `IAuthenticationPolicy.StartHttpConnection`'s session: no credentials
  with anonymous off is `401 NoAuthenticationInformation`, a refused token `401
  InvalidAuthenticationInfo`, both with the Bearer challenge; another `Authorization` scheme
  (Shared Key included, until BL-717) is `400 InvalidAuthenticationInfo`; a bearer request naming
  a version before 2017-11-09 is `403 AuthenticationFailed`; an anonymous write is `401`.
- Every other request: `405 UnsupportedHttpVerb` for a method other than `GET`/`HEAD`;
  `400 InvalidQueryParameterValue` for `comp`, `restype`, `snapshot` or `versionid`; `400 InvalidUri`
  for a path naming no blob; `400 UnsupportedHeader` for a lease, copy, customer-key, ACL or similar
  header; `400 InvalidResourceName` for a blob name over 1024 characters.
- Every answer carries `Date`, `Server`, `x-ms-request-id` (a lower-case GUID), `x-ms-version:
  2025-01-05` and the echoed `x-ms-client-request-id`; an error carries `x-ms-error-code` and the
  one-line XML `Error` body, whose `Message` ends with `RequestId:` and `Time:` lines. A `HEAD`
  error has no body. A connection past a limit is `503 ServerBusy`, then closed.

Not yet: Put Blob, List Blobs, Delete Blob (BL-425), the Data Lake schemes (BL-426), Shared Key and
SAS (BL-717), blocks (BL-715), List Containers and properties (BL-716), credentials checked under
`--allow-anonymous` and the path-style account segment (BL-721), and `400 InvalidInput` for a head
past `--max-request-head`, which `Surl.HttpServing` still answers `431` (BL-722).

This library references `Surl.Protocol.Abstractions`, `Surl.Content` and `Surl.HttpServing`
(and through it `Surl.HttpMessage`), nothing else (`ProtocolIsolationTests`). It never
constructs a transport type. Tests replay request bytes written from the REST API pages through
`InMemoryConnection` on an `InMemoryContentFileSystem`, with a hand-written bearer policy.
