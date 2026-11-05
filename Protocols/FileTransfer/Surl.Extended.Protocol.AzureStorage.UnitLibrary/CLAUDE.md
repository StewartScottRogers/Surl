# Surl.Extended.Protocol.AzureStorage.UnitLibrary

The Azure Storage server (ADR-0109): answers the Azure Blob service REST API on `az` and the Data
Lake Storage Gen2 endpoint on `abfs` and `abfss`, over HTTP/1.1 on the shared `ContentStore`, whose
top-level folders are the containers and filesystems (ADR-0100 decision 2). An extended server
(ADR-0083): judged by the Azure Storage REST API reference at `x-ms-version: 2025-01-05`, never by
upstream curl (which has none of these schemes) and never by the Curl port. Namespace
`Surl.Extended.Protocol.AzureStorage`.

The listen scheme (`ExchangeContext.Scheme`) decides the endpoint: `AzureBlobRequestAnswerer` on
`az`, `AzureDataLakeRequestAnswerer` on `abfs` and `abfss`. Both log in through `AzureLoginJudge`.

What the Blob endpoint serves now (BL-424, BL-425), on `az`:

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
- Put Blob (BL-425, `AzurePutBlob`) on `PUT /<container>/<blob>`: needs `--allow-uploads` (off:
  `403 AuthorizationPermissionMismatch`) and `x-ms-blob-type: BlockBlob` (missing `400
  MissingRequiredHeader`, `AppendBlob`/`PageBlob` `400 UnsupportedHeader`, another value `400
  InvalidHeaderValue`). Before the body: `Content-MD5` not base64 of 16 bytes `400 InvalidMd5`, an
  `x-ms-meta-*` name that is no C# identifier `400 InvalidMetadata`, metadata past 8 KiB `400
  MetadataTooLarge`, an unreadable body `400 InvalidInput`, no `Content-Length` and no chunked body
  `411 MissingContentLengthHeader`, a length past `--max-filesize` `413 RequestBodyTooLarge`. The
  body streams into a content-store upload session (`AzureUploadStream`), creating the name's
  folders (a file in the way, or a folder at the name, `409 InvalidResourceName`); a body cut short
  is `400 InvalidInput`, one not matching `Content-MD5` `400 Md5Mismatch`, and neither stores
  anything. A zero-byte name ending in `/` creates the folder. `x-ms-blob-content-type` (else
  `Content-Type`), the other `x-ms-blob-*` fields and each `x-ms-meta-*` go to `ObjectMetadataIndex`
  under `AzureIndexKey` (none for a file in a share); an index refusal removes the blob (`400
  MetadataTooLarge`, `503 ServerBusy`). `201` with `ETag`, `Last-Modified`, `Content-MD5` and
  `x-ms-request-server-encrypted: false`.
- Delete Blob on `DELETE /<container>/<blob>`: `202`; a missing blob `404 BlobNotFound`; uploads
  off or share access refusing it `403 AuthorizationPermissionMismatch`.
- List Blobs (`AzureListBlobs`) on `GET /<container>?restype=container&comp=list`: a listing in
  ADR-0006's sense, as ADR-0108 holds for S3's, so it needs `--list-directories` (off: `403
  AuthorizationPermissionMismatch`). `AzureContainerWalk` yields blob names in UTF-8 byte order;
  `AzureListPage` takes `maxresults` (default and cap 5000, below 1 `400
  OutOfRangeQueryParameterValue`, no integer `400 InvalidQueryParameterValue`) blobs and
  `BlobPrefix`es after the `marker` (`AzureListMarker`: base64url of the last name returned).
  `prefix` and `delimiter` filter and roll up; `include=metadata` adds `Metadata`, the page's other
  `include` values add nothing, any other is `400 InvalidQueryParameterValue`. The
  `EnumerationResults` follows the page's example: `ServiceEndpoint` (`https` over TLS, else
  `http`, and the `Host`), `ContainerName`, the sent `Prefix`, `Marker`, `MaxResults` and
  `Delimiter`, each blob's `Properties` in decision 5.3's order (empty elements for no value,
  `Etag` without its quotes), and `NextMarker`, empty on the last page.
- Every other request: `405 UnsupportedHttpVerb` for a method other than `GET`, `HEAD`, `PUT` or `DELETE`;
  `400 InvalidQueryParameterValue` for `comp`, `restype`, `snapshot` or `versionid`; `400 InvalidUri`
  for a path naming no blob; `400 UnsupportedHeader` for a lease, copy, customer-key, ACL or similar
  header; `400 InvalidResourceName` for a blob name over 1024 characters.
- Every answer carries `Date`, `Server`, `x-ms-request-id` (a lower-case GUID), `x-ms-version:
  2025-01-05` and the echoed `x-ms-client-request-id`; an error carries `x-ms-error-code` and the
  one-line XML `Error` body, whose `Message` ends with `RequestId:` and `Time:` lines. A `HEAD`
  error has no body. A connection past a limit is `503 ServerBusy`, then closed.

What the Data Lake endpoint serves now (BL-426), on `abfs` and `abfss`, in the same order of checks
(required headers, login, operation, `404 FilesystemNotFound`, `400 InvalidResourceName` past 1024
characters):

- Path - Read (`GET`, and its head with `HEAD`) on `/<filesystem>/<path>`: Get Blob's answer with
  `x-ms-resource-type: file` in place of the blob, lease and encryption fields; a directory `409
  ResourceTypeMismatch`; no file `404 PathNotFound`.
- Path - Create (`AzurePathCreate`), `PUT …?resource=file|directory`, under `--allow-uploads`:
  creates every missing parent; `file` writes the file empty (emptying one there), discards its
  staged appends and keeps `x-ms-content-type`, `-encoding`, `-language`, `-disposition` and
  `x-ms-cache-control` in `ObjectMetadataIndex`. `201` with `ETag` and `Last-Modified`; a directory's
  ETag is `"0x<last-write ticks in hexadecimal>"`. A file in the way, a file over a directory or a
  directory over a file is `409 PathConflict`.
- Path - Update (`AzurePathUpdate`), `PATCH …?action=append|flush&position=<n>`, under
  `--allow-uploads`: an append writes its body at `n` into the file's `AzureStagedPath` - a
  `ContentUploadSession` started from the committed bytes, so nothing is visible until the flush -
  and answers `202`; `flush=true` on an append also flushes. A flush commits the staged bytes up to
  `n` when the staged ranges reach it from the committed length without a gap or an overlap (else
  `400 InvalidFlushPosition`), keeps the stored header fields, and answers `200` with the new
  `ETag`. A flush with a body is `400 ContentLengthMustBeZero`; an append before the committed end
  or `retainUncommittedData=true` is `400 InvalidQueryParameterValue`. `AzurePathStagingArea` holds
  at most 1000 staged files (the next `503 ServerBusy`), drops one untouched for 7 days when the next
  staging begins, and serves appends and flushes one at a time across the server, because an upload
  session is not safe for concurrent use.
- Path - Delete, `DELETE /<filesystem>/<path>`, under `--allow-uploads`: a file, an empty directory,
  or with `recursive=true` a directory and all it holds; `200`. A non-empty directory without it is
  `409 DirectoryNotEmpty`; a missing path `404 PathNotFound`.
- Path - List (`AzurePathList`, `AzureFilesystemWalk`), `GET /<filesystem>?resource=filesystem`,
  under `--list-directories`: `recursive` required, `directory`, `maxResults` (1 to 5000) and
  `continuation` (base64url of the last sort key, a directory's being its path and `/`); `{"paths":[…]}`
  in decision 5.4's member order, `x-ms-continuation` while more remain.
- Errors are the JSON `{"error":{"code":…,"message":…}}` (`AzureDataLakeErrorBody`) with
  `Content-Type: application/json;charset=utf-8`; `code` and `message` only.

Not yet: the conditional fields on Put Blob and Delete Blob and `x-ms-blob-content-md5` (filed
from BL-425); `retainUncommittedData=true`, `beginFrom`, `x-ms-properties`, the conditional fields on
the Data Lake writes and the JSON body on a Data Lake connection refused past a limit (BL-758); Shared
Key and
SAS (BL-717), blocks (BL-715), List Containers and properties (BL-716), credentials checked under
`--allow-anonymous` and the path-style account segment (BL-725), and `400 InvalidInput` for a head
past `--max-request-head`, which `Surl.HttpServing` still answers `431` (BL-726).

This library references `Surl.Protocol.Abstractions`, `Surl.Content` and `Surl.HttpServing`
(and through it `Surl.HttpMessage`), nothing else (`ProtocolIsolationTests`). It never
constructs a transport type. Tests replay request bytes written from the REST API pages through
`InMemoryConnection` on an `InMemoryContentFileSystem`, with a hand-written bearer policy.
