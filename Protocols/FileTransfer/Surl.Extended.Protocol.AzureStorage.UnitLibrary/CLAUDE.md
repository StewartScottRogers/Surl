# Surl.Extended.Protocol.AzureStorage.UnitLibrary

The Azure Storage server (ADR-0109): answers the Azure Blob service REST API on `az` and the Data
Lake Storage Gen2 endpoint on `abfs` and `abfss`, over HTTP/1.1 on the shared `ContentStore`, whose
top-level folders are the containers and filesystems (ADR-0100 decision 2). An extended server
(ADR-0083): judged by the Azure Storage REST API reference at `x-ms-version: 2025-01-05`, never by
upstream curl (which has none of these schemes) and never by the Curl port. Namespace
`Surl.Extended.Protocol.AzureStorage`.

The listen scheme (`ExchangeContext.Scheme`) decides the endpoint: `AzureBlobRequestAnswerer` on
`az`, `AzureDataLakeRequestAnswerer` on `abfs` and `abfss`. Both log in through `AzureLoginJudge`.

What the Blob endpoint serves now (BL-424, BL-425, BL-715, BL-716), on `az`:

- Get Blob and Get Blob Properties on `/<container>/<blob>` (account-host form): one `Range` or
  `x-ms-range` (`x-ms-range` wins; `206`, `416 InvalidRange`, several ranges ignored),
  `x-ms-range-get-content-md5` (`400 OutOfRangeInput` past 4 MiB), the conditional fields
  (`412 ConditionNotMet`, `304`), and the blob's stored header fields and `x-ms-meta-*` from
  `ObjectMetadataIndex` (the index's defaults, computed here, for a file the index does not key).
  `Content-MD5` is the `x-ms-blob-content-md5` stored with the upload (BL-736), else the base64 of
  the ETag when the ETag is the index's quoted hexadecimal MD5.
- The required headers: a malformed `x-ms-version` or `x-ms-date` is `400 InvalidHeaderValue`; a
  request with `Authorization` and no `x-ms-version` is `400 MissingRequiredHeader`.
- The login (BL-424, BL-717) through `IAuthenticationPolicy.StartHttpConnection`'s session:
  `AzureLoginJudge` sends each request with its `AzureStorageScope` - the `Host`'s first label (or the
  path-style segment, below) as the account a shared access signature names, the decoded resource path, the SAS permission
  letters that grant the operation (`GrantingPermissionsOf`: `l` a listing `GET`, `r` any other read, `d` a
  delete, `aw` a Data Lake append, `w` any other write; `c` alone never grants a write) and the
  peer's address (`AzureStorageProtocolServer.PeerAddressOf`) - and `Surl.Authentication` judges
  the bearer token, `SharedKey`/`SharedKeyLite` signature or shared access signature. Each
  refusal's `HttpAuthenticationRefusal` is answered with ADR-0109 decision 3.4's code: no
  credentials `401 NoAuthenticationInformation`, a refused bearer token `401
  InvalidAuthenticationInfo`, both with the Bearer challenge; another scheme or credentials that
  do not read `400 InvalidAuthenticationInfo`; a permission a signature does not grant `403
  AuthorizationPermissionMismatch`; every other `403 AuthenticationFailed` with a fixed
  `AuthenticationErrorDetail` (an unknown account reads `Signature did not match.`, as a wrong
  signature does). A bearer request naming a version before 2017-11-09 is `403
  AuthenticationFailed` before the session is asked; an anonymous write is `401`.
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
  under `AzureIndexKey` (none for a file in a share), and `x-ms-blob-content-md5`, kept as sent and
  never checked, becomes the blob's `Content-MD5` on Get Blob, Get Blob Properties and List Blobs
  (BL-736; a rename and Path - Update `setProperties` keep it, a flush drops it); an index refusal removes the blob (`400
  MetadataTooLarge`, `503 ServerBusy`). `201` with `ETag`, `Last-Modified`, `Content-MD5` and
  `x-ms-request-server-encrypted: false`.
- The conditional fields on Put Blob, Put Block List (BL-944, checked after the body is read and before the blocks are resolved) and Delete Blob (BL-735, `AzureConditions.WriteRefusal`),
  checked against the blob at the name once the name maps, before folders are created or the body
  is read: every condition counts, none decides over another. On an existing blob, `If-None-Match`
  naming `*` is `409 BlobAlreadyExists`; a failing `If-Match` or `If-Unmodified-Since`, a matching
  `If-None-Match` or an unmodified `If-Modified-Since` is `412 ConditionNotMet`; nothing changes.
  With no blob there, Put Blob fails only `If-Match` (`412`), and Delete Blob is `404 BlobNotFound`.
  A date that does not parse is ignored.
- Put Block, Put Block List and Get Block List (BL-715). Put Block (`AzurePutBlock`) on `PUT
  …?comp=block&blockid=<id>`, under `--allow-uploads`: a missing `blockid` is `400
  MissingRequiredQueryParameter`; one that is not base64 of 1 to 64 bytes (`AzureBlockId`), or not
  as long as the blob's other block IDs, `400 InvalidBlockId`; past 100000 uncommitted blocks `409
  BlockCountExceedsLimit`. Put Blob's `Content-MD5`, framing and `--max-filesize` checks apply. The
  body is appended to the blob's `AzureStagedBlocks` - an upload session opened at the blob and
  never committed, so its bytes sit in a hidden dot-file beside the blob (the name's folders are
  created at the first block) and the blob stays invisible - and the block is recorded, a block
  put again under the same ID replacing the old one. The staged bytes together are held to
  `--max-filesize`: past it every uncommitted block of the blob is discarded and the answer is `413
  RequestBodyTooLarge`. `201` with the body's `Content-MD5`. Put Block List (`AzurePutBlockList`)
  on `PUT …?comp=blocklist`, under `--allow-uploads`, with Put Blob's head checks: the body
  (`AzureBlockListDocument`, at most 4 MiB through `AzureBoundedBodyStream`, else `400
  InvalidXmlDocument`; no DTD) is a `BlockList` of `Committed`, `Uncommitted` and `Latest` entries,
  at most 50000 (`400 BlockListTooLong`). Each is looked for in its list (`Latest`: uncommitted,
  then committed); one not found is `400 InvalidBlockList` and nothing changes. The blob is written
  afresh through an upload session from the blocks in order - staged bytes from the staging
  session, committed bytes from the blob itself - its `x-ms-blob-*` and `x-ms-meta-*` kept as Put
  Blob keeps them, the uncommitted blocks discarded and the new committed list remembered against
  the file's length and last-write time (a blob written any other way since has no committed
  blocks). `201` with `ETag` (the blob's MD5), `Last-Modified` and
  `x-ms-request-server-encrypted: false`. Get Block List (`AzureGetBlockList`) on `GET
  …?comp=blocklist`, `blocklisttype` `committed` (the default), `uncommitted` or `all`
  (case-insensitive; another `400 InvalidQueryParameterValue`): `200` with the one-line
  `BlockList` XML of the page's example, `Last-Modified` and `ETag` when the blob exists, and
  `x-ms-blob-content-length`; no blob and no uncommitted block is `404 BlobNotFound`.
- Delete Blob on `DELETE /<container>/<blob>`: `202`; a missing blob `404 BlobNotFound`; uploads
  off or share access refusing it `403 AuthorizationPermissionMismatch`.
- List Blobs (`AzureListBlobs`) on `GET /<container>?restype=container&comp=list`: a listing in
  ADR-0006's sense, as ADR-0108 holds for S3's, so it needs `--list-directories` (off: `403
  AuthorizationPermissionMismatch`). `AzureContainerWalk` yields blob names in UTF-8 byte order;
  `AzureListPage` takes `maxresults` (default and cap 5000, below 1 `400
  OutOfRangeQueryParameterValue`, no integer `400 InvalidQueryParameterValue`) blobs and
  `BlobPrefix`es after the `marker` (`AzureListMarker`: base64url of the last name returned).
  `prefix` and `delimiter` filter and roll up; `include=metadata` adds `Metadata`;
  `include=uncommittedblobs` (BL-923) also lists each blob whose `AzureStagedBlocks` holds uncommitted
  blocks and that has no file, merged in name order (`AzureStagingArea.UncommittedBlobs`,
  `AzureListBlobs.Merged`), with `Content-Length` 0, an empty `Etag` and content fields, and the last
  block's time; the page's other
  `include` values add nothing, any other is `400 InvalidQueryParameterValue`. The
  `EnumerationResults` follows the page's example: `ServiceEndpoint` (`https` over TLS, else
  `http`, and the `Host`), `ContainerName`, the sent `Prefix`, `Marker`, `MaxResults` and
  `Delimiter`, each blob's `Properties` in decision 5.3's order (empty elements for no value,
  `Etag` without its quotes), and `NextMarker`, empty on the last page.
- List Containers (BL-716, `AzureListContainers`) on `GET /?comp=list`, under `--list-directories`:
  the containers the caller may read (`AzureTopLevelFolders`: the root's top-level folders, shares
  included, and every `--bucket`, a hidden share or a top-level file left out) in UTF-8 byte order,
  `prefix`, `marker` and `maxresults` read as List Blobs reads them; `include=metadata` adds an
  empty `Metadata`, `deleted` and `system` add nothing, another value is `400
  InvalidQueryParameterValue`. Each `Container` has `Name` and `Properties` (`Last-Modified`, the
  folder's `Etag` without quotes, `LeaseStatus` `unlocked`, `LeaseState` `available`,
  `HasImmutabilityPolicy` and `HasLegalHold` `false`). Get Container Properties
  (`AzureFolderProperties`) on `GET` or `HEAD /<container>?restype=container` (no `comp`): `200`, no
  body, the folder's `ETag` (`"0x<ticks>"`) and `Last-Modified`, and the lease, immutability and
  legal-hold fields.
- Every other request: `405 UnsupportedHttpVerb` for a method other than `GET`, `HEAD`, `PUT` or `DELETE`;
  `400 InvalidQueryParameterValue` for `comp` (other than the three block requests), `restype` (other than the container requests above), `snapshot` or `versionid`; `400 InvalidUri`
  for a path naming no blob; `400 UnsupportedHeader` for a lease, copy, customer-key, ACL or similar
  header; `400 InvalidResourceName` for a blob name over 1024 characters.
- Every answer carries `Date`, `Server`, `x-ms-request-id` (a lower-case GUID), `x-ms-version:
  2025-01-05` and the echoed `x-ms-client-request-id`; an error carries `x-ms-error-code` and the
  one-line XML `Error` body, whose `Message` ends with `RequestId:` and `Time:` lines. A `HEAD`
  error has no body. A connection past a limit is `503 ServerBusy`, then closed: the XML `Error` on
  `az`, the JSON error on `abfs` and `abfss` (BL-758), told apart by the listen scheme the engine
  passes to `IConnectionRefusalWriter`'s scheme-aware overload. A head past
  `--max-request-head` (BL-726) is `400 InvalidInput` with the common fields, `x-ms-error-code` and
  the `Error` body - on `abfs` and `abfss` the JSON error body - then closed: both answerers word
  `Surl.HttpServing`'s refusal through `IHttpHeadNotReadRefuser` (`AzureReply.HeadNotReadRefusal`);
  every other head not read keeps the standard `505`, `408` or `400`.

What the Data Lake endpoint serves now (BL-426, BL-716), on `abfs` and `abfss`, in the same order of checks
(required headers, login, operation, `404 FilesystemNotFound`, `400 InvalidResourceName` past 1024
characters):

- Path - Read (`GET`) and Path - Get Properties (`HEAD`, `action` absent or `getStatus`; another
  `action`, such as `getAccessControl`, `400 InvalidQueryParameterValue`) on `/<filesystem>/<path>`:
  Get Blob's answer with `x-ms-resource-type: file` and, when the file has user metadata,
  `x-ms-properties` (`AzurePropertiesField`) in place of the blob, lease and encryption fields; a
  directory's `HEAD` is `200` with its `ETag`, `Last-Modified` and `x-ms-resource-type: directory`
  (BL-716), its `GET` `409 ResourceTypeMismatch`; no file `404 PathNotFound`.
- Filesystem - List (BL-716, `AzureFilesystemList`) on `GET /?resource=account`, under
  `--list-directories`: `{"filesystems":[…]}` with each `name`, `lastModified` and `etag`, from
  `AzureTopLevelFolders` as List Containers lists them; `prefix`, `maxResults` (1 to 5000) and
  `continuation` (base64url of the last name), `x-ms-continuation` while more remain. Filesystem -
  Get Properties on `HEAD /<filesystem>?resource=filesystem`: `200`, `ETag`, `Last-Modified` and
  `x-ms-namespace-enabled: true`.
- Rename (BL-716, `AzurePathRename`): `PUT /<filesystem>/<path>` with `x-ms-rename-source:
  /<filesystem>/<path>` (its query ignored; `resource` not needed; `mode` accepted), under
  `--allow-uploads`, moves the file or directory through `ContentStore.RenameEntry` (or
  `RenameEntryWithoutReplacing` with `If-None-Match: *`), a renamed file's stored header fields and
  properties stored again under the new name; `201` with `ETag` and `Last-Modified`. A source naming
  no path `400 InvalidSourceUri`; nothing there `404 SourcePathNotFound`; a directory into itself
  `400 InvalidRenameSourcePath`; no parent directory `404 RenameDestinationParentPathNotFound`; a
  directory in the way (or anything, with `If-None-Match: *`) `409 PathAlreadyExists`; a rename the
  store does not permit `403 AuthorizationPermissionMismatch`.
- Path - Update `action=setProperties` (BL-716, `AzurePathSetProperties`), under `--allow-uploads`:
  `x-ms-properties` (`name=base64,…`, names C# identifiers, values UTF-8; else `400
  InvalidHeaderValue`) replaces the file's user metadata in `ObjectMetadataIndex` - the same entries
  the Blob endpoint sends as `x-ms-meta-*` - and each `x-ms-content-*` or `x-ms-cache-control` sent
  replaces the stored field; `200` with `ETag` and `Last-Modified`. A directory is `409
  ResourceTypeMismatch` (the index keeps files only), a missing path `404 PathNotFound`, metadata
  past 8 KiB `400 MetadataTooLarge`; a file in a share keeps nothing.
- Path - Create (`AzurePathCreate`), `PUT …?resource=file|directory`, under `--allow-uploads`:
  creates every missing parent; `file` writes the file empty (emptying one there), discards its
  staged appends and keeps `x-ms-content-type`, `-encoding`, `-language`, `-disposition`,
  `x-ms-cache-control` and `x-ms-properties` (BL-758; one that does not read `400
  InvalidHeaderValue`) in `ObjectMetadataIndex`. The conditional fields are checked first against
  the file or directory at the path (`AzurePathCreate.ConditionRefusalAsync`, BL-758): `If-None-Match: *`
  on one there is `409 PathAlreadyExists`, any other failing condition `412 ConditionNotMet`. `201` with `ETag` and `Last-Modified`; a directory's
  ETag is `"0x<last-write ticks in hexadecimal>"`. A file in the way, a file over a directory or a
  directory over a file is `409 PathConflict`.
- Path - Update (`AzurePathUpdate`), `PATCH …?action=append|flush&position=<n>`, under
  `--allow-uploads`: an append writes its body at `n` into the file's `AzureStagedPath` - a
  `ContentUploadSession` started from the committed bytes, so nothing is visible until the flush -
  and answers `202`; `flush=true` on an append also flushes. A flush commits the staged bytes up to
  `n` when the staged ranges reach it from the committed length without a gap or an overlap (else
  `400 InvalidFlushPosition`), keeps the stored header fields, and answers `200` with the new
  `ETag`. A flush with a body is `400 ContentLengthMustBeZero`; an append before the committed end
  is `400 InvalidQueryParameterValue`. A flush with `retainUncommittedData=true` (BL-758) copies the
  staging's session whole before committing and carries the staging on in the copy
  (`AzureStagedPath.ContinueAfterFlush`), so the bytes staged past the flush wait for a later one.
  Append and flush check the conditional fields against the committed file as Path - Create does. `AzureStagingArea`, shared with the
  blobs' `AzureStagedBlocks`, holds at most 1000 stagings (the next `503 ServerBusy`, unless one with only a
  remembered committed block list can be forgotten), drops one untouched for 7 days when the next
  staging begins, and serves appends, flushes and block requests one at a time across the server, because an upload
  session is not safe for concurrent use.
- Path - Delete, `DELETE /<filesystem>/<path>`, under `--allow-uploads`: a file, an empty directory,
  or with `recursive=true` a directory and all it holds; `200`. A non-empty directory without it is
  `409 DirectoryNotEmpty`; a missing path `404 PathNotFound`.
- Path - List (`AzurePathList`, `AzureFilesystemWalk`), `GET /<filesystem>?resource=filesystem`,
  under `--list-directories`: `recursive` required, `directory`, `maxResults` (1 to 5000) and
  `continuation` (base64url of the last sort key, a directory's being its path and `/`) and
  `beginFrom` (BL-758: a path relative to the folder listed, started after; a continuation decides
  over it); `{"paths":[…]}`
  in decision 5.4's member order, `x-ms-continuation` while more remain.
- Errors are the JSON `{"error":{"code":…,"message":…}}` (`AzureDataLakeErrorBody`) with
  `Content-Type: application/json;charset=utf-8`; `code` and `message` only.

The chosen-error fault (BL-981, ADR-0124 decisions 4 and 9), on both endpoints: once a request has
logged in, and before its operation is recognized or the content store is touched,
`AzureRequestFaults` asks `ExchangeContext.Faults` at the `Command` stage (through
`HttpCommandFaults`) with the scheme, the login as `account` (none when anonymous), the target's
path as sent (path-style account segment removed, query excluded) as `path` and the HTTP method as
`command`. An `error` rule whose reply is a status 400 to 599, one space and a code word of 1 to 64
ASCII letters and digits (`503 ServerBusy`) is answered with that status (the standard reason
phrase, or none for a code without one), `x-ms-error-code` and the endpoint's error body - the XML
`Error` on `az`, the JSON error on `abfs` and `abfss`, none for a `HEAD` - whose message is
`AzureRequestFaults.ChosenErrorMessage`, and publishes one `FaultAppliedEvent` naming the path. A
reply that does not fit, or a rule of another kind, is noted in the exchange log and the request is
served as without it.

Not yet: properties on a directory or a filesystem.

The path-style form (BL-725, ADR-0109 decision 2), on both endpoints: `AzurePathStyle.Read` runs
before anything else. When the `Host` (port removed) is an IP literal (`127.0.0.1`, `[::1]`; four
dotted parts for IPv4) or `localhost`, and the path's first segment, percent-decoded, is a named
account's name (`IAuthenticationPolicy.NamesAccount`), that segment is removed and the rest of the
path, with the query, is the target every operation reads. `AzureLoginJudge` then names that account
in the `AzureStorageScope` (so a shared access signature on an IP-literal host is checked under it)
and the resource path after it, while the session still receives the target as sent, which a Shared
Key signature's canonicalized resource covers (`/<account>/<account>/<container>/<blob>`). A segment
naming no account, or any other `Host`, leaves the target as it is.

This library references `Surl.Protocol.Abstractions`, `Surl.Content` and `Surl.HttpServing`
(and through it `Surl.HttpMessage`), nothing else (`ProtocolIsolationTests`). It never
constructs a transport type. Tests replay request bytes written from the REST API pages through
`InMemoryConnection` on an `InMemoryContentFileSystem`, with a hand-written bearer policy whose
`Verdict` scripts a Shared Key or SAS answer; the signatures themselves are tested in
`Surl.Authentication.UnitTests`' `AzureStorageVerifierTests`.
