# Azure Storage requirement matrix

The server-side requirements `Surl.Extended.Protocol.AzureStorage` answers, for the `az` scheme (the
Blob service) and the `abfs` and `abfss` schemes (the Data Lake Storage Gen2 endpoint) (ADR-0083
decision 9). Written from the Azure Storage REST API reference at `x-ms-version: 2025-01-05`
(<https://learn.microsoft.com/rest/api/storageservices/>) and the pages
[ADR-0109](../../Planning/Decisions/ADR-0109-how-the-azure-storage-server-answers-az-abfs-and-abfss.md)
decides by, never from the Curl port's client matrices. Each row binds the server; "Covered by"
names the task that proves it, until that task replaces its ID with the named test, or says why the
row needs no test.

The pages state few requirements in RFC keywords, so each response element a server writes, each
rule of a page and each error a server owes is a row. IDs are `AzureStorage:<Page>-<n>` for a REST API
page and `AzureStorage:Surl-<Decision>-<n>` for a rule ADR-0109 adds where the pages leave the server a
choice.

## Errors and common headers ("Status and error codes", "Common REST API error codes", `DataLakeStorageError`)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:StatusAndErrorCodes-1 | MUST | A Blob endpoint failure is a 3xx, 4xx or 5xx status with an `application/xml` `Error` body holding `Code` and `Message`. | BL-424 |
| AzureStorage:StatusAndErrorCodes-2 | MUST | Every error carries `x-ms-error-code` naming its code. | BL-424 |
| AzureStorage:StatusAndErrorCodes-3 | MUST | Each code is sent with the status its table gives it (ADR-0109 decision 5.2's table). | BL-424 |
| AzureStorage:StatusAndErrorCodes-4 | MUST | `MissingRequiredHeader` and `UnsupportedHeader` carry `HeaderName`; `InvalidHeaderValue` carries `HeaderName` and `HeaderValue`; the query codes carry `QueryParameterName`, `QueryParameterValue` and `Reason`. | BL-424 |
| AzureStorage:StatusAndErrorCodes-5 | MUST | A `HEAD` error has its status, `x-ms-error-code` and no body. | BL-424 |
| AzureStorage:DataLakeStorageError-1 | MUST | A Data Lake endpoint failure has a JSON body `{"error":{"code":…,"message":…}}` and `x-ms-error-code`. | BL-426 |
| AzureStorage:DataLakeStorageError-2 | MUST | Each Data Lake code is sent with the status and message the Data Lake pages list. | BL-426 |
| AzureStorage:CommonResponseHeaders-1 | MUST | Every answer carries `x-ms-request-id` and `x-ms-version: 2025-01-05`. | BL-424 |
| AzureStorage:CommonResponseHeaders-2 | MUST | `x-ms-client-request-id`, when sent and at most 1024 visible ASCII bytes, is echoed. | BL-424 |
| AzureStorage:Surl-Errors-1 | MUST | `Code` and the first line of `Message` are fixed per code; `RequestId:` and `Time:` lines follow from the server's own ID and clock. | BL-424 |
| AzureStorage:Surl-Errors-2 | MUST | Client bytes appear only in `HeaderValue` and `QueryParameterValue`, XML-escaped and cut to 1024 bytes; the Data Lake body echoes none. | BL-424 |
| AzureStorage:Surl-Errors-3 | MUST | `AuthenticationErrorDetail` is a fixed sentence and never the computed string to sign. | BL-717 |
| AzureStorage:Surl-Operations-1 | MUST | A method the resource does not take is `405 UnsupportedHttpVerb`, judged after authentication. | BL-424 |
| AzureStorage:Surl-Operations-2 | MUST | An unserved `comp`, `restype`, `resource` or `action` value is `400 InvalidQueryParameterValue`. | BL-424 |
| AzureStorage:Surl-Operations-3 | MUST | A header asking for an unserved feature (copy, append or page blob, lease, customer key, encryption scope, ACL, owner, umask, structured body, expiry) is `400 UnsupportedHeader`. | BL-424 |
| AzureStorage:Surl-Operations-4 | MUST | Create and Delete Container, Filesystem - Create and Filesystem - Delete are refused as an unserved `restype` or `resource`. | BL-424 |

## Versioning and required headers ("Versioning for the Azure Storage services", each operation's "Request headers")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:Versioning-1 | MUST | An authenticated request without `x-ms-version` is `400 MissingRequiredHeader`, `HeaderName` `x-ms-version`. | BL-424 |
| AzureStorage:Versioning-2 | MUST | An anonymous request without `x-ms-version` is served. | BL-424 |
| AzureStorage:Versioning-3 | MUST | An `x-ms-version` that is not a `yyyy-MM-dd` date is `400 InvalidHeaderValue`. | BL-424 |
| AzureStorage:Surl-Versioning-1 | MUST | Any well-formed version is answered with 2025-01-05's bytes and names 2025-01-05. | BL-424 |
| AzureStorage:RequestHeaders-1 | MUST | An `x-ms-date` that is not an RFC 1123 time in GMT is `400 InvalidHeaderValue`. | BL-424 |
| AzureStorage:RequestHeaders-2 | MUST | A Put Blob, Put Block, Put Block List or Path - Update without `Content-Length` is `411 MissingContentLengthHeader`. | BL-425 |
| AzureStorage:RequestHeaders-3 | MUST | A missing required query parameter (`position`, `recursive`, `blockid`) is `400 MissingRequiredQueryParameter`. | BL-426 |

## Addressing and names ("Naming and Referencing Containers, Blobs, and Metadata"; ADR-0100 decision 2)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:Surl-Addressing-1 | MUST | The listen scheme decides the endpoint: `az` is Blob, `abfs` and `abfss` are Data Lake; the `Host` never switches it. | BL-424 |
| AzureStorage:Surl-Addressing-2 | MUST | In the account-host form the path is `/<container>/<blob>` or `/<filesystem>/<path>` whatever the `Host`; `/` is the account. | BL-424 |
| AzureStorage:NamingAndReferencing-1 | MUST | With an IP-literal or `localhost` host, a first path segment naming a named account is the account and is removed. | BL-424 |
| AzureStorage:NamingAndReferencing-2 | MUST | A blob name over 1024 characters is `400 InvalidResourceName`. | BL-424 |
| AzureStorage:NamingAndReferencing-3 | MUST | The blob name is percent-decoded once from the path, as UTF-8. | BL-424 |
| AzureStorage:Surl-Store-1 | MUST | A container or filesystem is a top-level folder of the shared content store; a top-level file and `.surl` are not. | BL-424 |
| AzureStorage:Surl-Store-2 | MUST | A name the store cannot hold is `400 InvalidResourceName` on a write and a missing blob or path on a read. | BL-425 |
| AzureStorage:Surl-Store-3 | MUST | A name conflicting with an entry of the other kind is `409 InvalidResourceName` (Blob) or `409 PathConflict` (Data Lake). | BL-425 |
| AzureStorage:Surl-Store-4 | MUST | A file another protocol wrote is served with the metadata index's default `Content-Type`, `Content-MD5` and ETag. | BL-425 |
| AzureStorage:Surl-Store-5 | MUST | A container or blob the caller may not read is `404 ContainerNotFound` / `BlobNotFound` (or `FilesystemNotFound` / `PathNotFound`); a write it may not make is `403 AuthorizationPermissionMismatch`. | BL-425 |
| AzureStorage:Surl-Store-6 | MUST | A zero-byte Put Blob whose name ends in `/` creates the folder; a non-empty one is `400 InvalidResourceName`. | BL-425 |

## Get Blob and Get Blob Properties

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:GetBlob-1 | MUST | `GET /<container>/<blob>` answers `200` with the blob's bytes. | BL-424 |
| AzureStorage:GetBlob-2 | MUST | The answer carries `Content-Length`, `Content-Type`, `Content-MD5`, `ETag`, `Last-Modified`, `Accept-Ranges: bytes`, `x-ms-blob-type: BlockBlob`, `x-ms-lease-status`, `x-ms-lease-state`, `x-ms-server-encrypted` and each stored header and `x-ms-meta-*`. | BL-424 |
| AzureStorage:GetBlob-3 | MUST | A missing blob, or a folder, is `404 BlobNotFound`; a missing container `404 ContainerNotFound`. | BL-424 |
| AzureStorage:GetBlobProperties-1 | MUST | `HEAD /<container>/<blob>` answers Get Blob's head with no body. | BL-424 |
| AzureStorage:RangeHeader-1 | MUST | One `Range` or `x-ms-range` range is `206` with `Content-Range`; `x-ms-range` wins. | BL-424 |
| AzureStorage:RangeHeader-2 | MUST | An unsatisfiable range is `416 InvalidRange`; several ranges are ignored and the whole blob served. | BL-424 |
| AzureStorage:RangeHeader-3 | MUST | `x-ms-range-get-content-md5: true` adds the range's `Content-MD5` up to 4 MiB and is `400 OutOfRangeInput` past it. | BL-424 |
| AzureStorage:ConditionalHeaders-1 | MUST | On a read, `If-Match` or `If-Unmodified-Since` failing is `412 ConditionNotMet`; `If-None-Match` matching or `If-Modified-Since` not modified is `304`. | BL-424 |
| AzureStorage:ConditionalHeaders-2 | MUST | On a write, a failing condition is `412 ConditionNotMet`; `If-None-Match: *` on an existing blob is `409 BlobAlreadyExists`. | BL-425 |

## Put Blob, Delete Blob and List Blobs

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:PutBlob-1 | MUST | `PUT /<container>/<blob>` with `x-ms-blob-type: BlockBlob` stores the body, overwriting, and answers `201` with `ETag`, `Last-Modified` and `Content-MD5`. | BL-425 |
| AzureStorage:PutBlob-2 | MUST | A missing `x-ms-blob-type` is `400 MissingRequiredHeader`; `AppendBlob` or `PageBlob` `400 UnsupportedHeader`; another value `400 InvalidHeaderValue`. | BL-425 |
| AzureStorage:PutBlob-3 | MUST | A `Content-MD5` not base64 of 16 bytes is `400 InvalidMd5`; one not matching the body `400 Md5Mismatch`, and nothing is stored. | BL-425 |
| AzureStorage:PutBlob-4 | MUST | The `x-ms-blob-content-*` headers (else `Content-Type`), `x-ms-blob-cache-control` and `x-ms-meta-*` are stored and returned by Get Blob and Get Blob Properties. | BL-425 |
| AzureStorage:PutBlob-5 | MUST | An empty body with `Content-Length: 0` creates an empty blob. | BL-425 |
| AzureStorage:DeleteBlob-1 | MUST | `DELETE /<container>/<blob>` removes the blob and answers `202`; a missing blob is `404 BlobNotFound`. | BL-425 |
| AzureStorage:ListBlobs-1 | MUST | `GET /<container>?restype=container&comp=list` answers `EnumerationResults` with `ServiceEndpoint` and `ContainerName`, each `Blob`'s `Name` and `Properties` in the page's order, and `NextMarker`. | BL-425 |
| AzureStorage:ListBlobs-2 | MUST | Names are listed in ascending UTF-8 byte order. | BL-425 |
| AzureStorage:ListBlobs-3 | MUST | `prefix` limits the listing; `delimiter` rolls names up into `BlobPrefix` elements. | BL-425 |
| AzureStorage:ListBlobs-4 | MUST | `maxresults` defaults to and caps at 5000 and bounds blobs and prefixes together; 0 or below is `400 OutOfRangeQueryParameterValue`. | BL-425 |
| AzureStorage:ListBlobs-5 | MUST | A truncated page has a non-empty `NextMarker`; passing it as `marker` resumes after the last entry; the last page's `NextMarker` is empty. | BL-425 |
| AzureStorage:ListBlobs-6 | MUST | `include=metadata` adds each blob's `Metadata`. | BL-425 |
| AzureStorage:Surl-Listing-1 | MUST | `include` values naming features Surl lacks add nothing; an unknown value, or a marker that does not decode, is `400 InvalidQueryParameterValue`. | BL-425 |

## Put Block, Put Block List and Get Block List

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:PutBlock-1 | MUST | `PUT ?comp=block&blockid=<id>` stages the block, invisible to every protocol, and answers `201`. | BL-715 |
| AzureStorage:PutBlock-2 | MUST | A block ID that is not base64, longer than 64 bytes decoded, or of another length than the blob's other blocks is `400 InvalidBlockId`. | BL-715 |
| AzureStorage:PutBlockList-1 | MUST | `PUT ?comp=blocklist` with a `BlockList` of `Latest`, `Committed` and `Uncommitted` entries commits the blob in that order and answers `201`. | BL-715 |
| AzureStorage:PutBlockList-2 | MUST | A list naming an unstaged block is `400 InvalidBlockList`; past 50000 entries `400 BlockListTooLong`; a body that does not parse `400 InvalidXmlDocument`. | BL-715 |
| AzureStorage:PutBlock-3 | MUST | Past 100000 uncommitted or 50000 committed blocks is `409 BlockCountExceedsLimit`. | BL-715 |
| AzureStorage:GetBlockList-1 | MUST | `GET ?comp=blocklist&blocklisttype=committed|uncommitted|all` answers `BlockList` with `CommittedBlocks` and `UncommittedBlocks`. | BL-715 |
| AzureStorage:Surl-Blocks-1 | MUST | Staged blocks untouched for 7 days are dropped; 1000 blobs with staged data at most (`503 ServerBusy`). | BL-715 |
| AzureStorage:ListBlobs-7 | MUST | `include=uncommittedblobs` lists blobs with staged blocks and no commit. | BL-715 |

## Containers and filesystems

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:ListContainers-1 | MUST | `GET /?comp=list` answers `EnumerationResults` with each `Container`, honouring `prefix`, `maxresults` and `marker`. | BL-716 |
| AzureStorage:ListContainers-2 | MUST | Only containers the caller may read are listed. | BL-716 |
| AzureStorage:GetContainerProperties-1 | MUST | `GET` or `HEAD /<container>?restype=container` answers `200` with `ETag` and `Last-Modified`, or `404 ContainerNotFound`. | BL-716 |
| AzureStorage:FilesystemList-1 | MUST | `GET /?resource=account` answers `{"filesystems":[…]}` of the filesystems the caller may read. | BL-716 |
| AzureStorage:FilesystemGetProperties-1 | MUST | `HEAD /<filesystem>?resource=filesystem` answers `200`, or `404 FilesystemNotFound` with no body. | BL-716 |

## Data Lake paths ("Path - Create", "Path - Update", "Path - Read", "Path - List", "Path - Delete", "Path - Get Properties")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:PathRead-1 | MUST | `GET /<filesystem>/<path>` answers `200` with the file's committed bytes, `x-ms-resource-type: file` and the entity headers. | BL-426 |
| AzureStorage:PathRead-2 | MUST | A missing path is `404 PathNotFound`; a missing filesystem `404 FilesystemNotFound`; a directory `409 ResourceTypeMismatch`. | BL-426 |
| AzureStorage:PathRead-3 | MUST | One range is `206` with `Content-Range`; an unsatisfiable one `416 InvalidRange`. | BL-426 |
| AzureStorage:PathCreate-1 | MUST | `PUT ?resource=file` creates the file empty, or truncates an existing one, creating missing parents, and answers `201`. | BL-426 |
| AzureStorage:PathCreate-2 | MUST | `PUT ?resource=directory` creates the folder and missing parents and answers `201`. | BL-426 |
| AzureStorage:PathCreate-3 | MUST | `If-None-Match: *` on an existing path is `409 PathAlreadyExists`; a file over a folder or the reverse `409 PathConflict`. | BL-426 |
| AzureStorage:PathCreate-4 | MUST | The `x-ms-content-*` headers, `x-ms-cache-control` and `x-ms-properties` are stored in the metadata index. | BL-426 |
| AzureStorage:PathUpdate-1 | MUST | `PATCH ?action=append&position=<n>` stages the body at `n`, invisible to every protocol, and answers `202`. | BL-426 |
| AzureStorage:PathUpdate-2 | MUST | `PATCH ?action=flush&position=<n>` commits contiguous staged data up to `n` and answers `200` with `ETag` and `Last-Modified`. | BL-426 |
| AzureStorage:PathUpdate-3 | MUST | Staged data with a gap or overlap, or a `position` not equal to the length after it, is `400 InvalidFlushPosition`. | BL-426 |
| AzureStorage:PathUpdate-4 | MUST | A flush with a non-zero `Content-Length` is `400 ContentLengthMustBeZero`. | BL-426 |
| AzureStorage:PathUpdate-5 | MUST | Staged data past the flush position is dropped unless `retainUncommittedData=true`. | BL-426 |
| AzureStorage:PathUpdate-6 | MUST | `flush=true` on an append appends then flushes. | BL-426 |
| AzureStorage:PathUpdate-7 | MAY | `close` raises a change notification. | n/a: Surl has no change notifications; `close` is accepted and changes nothing (ADR-0109 decision 5.5) |
| AzureStorage:PathUpdate-8 | MUST | `action=setProperties` stores `x-ms-properties` and the content headers. | BL-716 |
| AzureStorage:PathUpdate-9 | MAY | `action=setAccessControl` and `setAccessControlRecursive` set POSIX owners and ACLs. | n/a: `400 InvalidQueryParameterValue`; the shared store holds no POSIX owner (ADR-0109 decision 1) |
| AzureStorage:PathList-1 | MUST | `GET /<filesystem>?resource=filesystem&recursive=<bool>` answers `{"paths":[…]}` with `name`, `isDirectory` (directories only), `contentLength`, `lastModified`, `etag`, `owner`, `group` and `permissions`, typed as the page's schema. | BL-426 |
| AzureStorage:PathList-2 | MUST | `recursive=false` lists one level; `directory` limits to that folder, and a missing one is `404 PathNotFound`. | BL-426 |
| AzureStorage:PathList-3 | MUST | `maxResults` defaults to and caps at 5000; past it `x-ms-continuation` is sent, and `continuation` resumes; the last page has no header. | BL-426 |
| AzureStorage:PathList-4 | MUST | `beginFrom` starts after the relative path; `upn` is accepted. | BL-426 |
| AzureStorage:PathDelete-1 | MUST | `DELETE /<filesystem>/<path>` removes a file or empty directory and answers `200`; a non-empty directory needs `recursive=true`, else `409 DirectoryNotEmpty`. | BL-426 |
| AzureStorage:PathGetProperties-1 | MUST | `HEAD /<filesystem>/<path>` answers the entity headers and `x-ms-resource-type` with no body. | BL-716 |
| AzureStorage:PathCreate-5 | MUST | `x-ms-rename-source` renames a file or directory through the content store; a missing source is `404 SourcePathNotFound`. | BL-716 |
| AzureStorage:PathLease-1 | MAY | Path - Lease and the `x-ms-lease-*` headers. | n/a: `400 UnsupportedHeader` or `400 InvalidQueryParameterValue`; a lease other protocols ignore would be a false promise (ADR-0109 decision 1) |

## Authentication ("Authorize with Microsoft Entra ID", "Authorize with Shared Key", "Create a service SAS", "Create an account SAS")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:AuthorizeEntra-1 | MUST | `Authorization: Bearer <token>` is served when the token is an empty-name account's password. | BL-424 |
| AzureStorage:AuthorizeEntra-2 | MUST | A bearer request with `x-ms-version` before 2017-11-09 is `403 AuthenticationFailed`. | BL-424 |
| AzureStorage:BearerChallenge-1 | MUST | No credentials with anonymous off is `401 NoAuthenticationInformation` with `WWW-Authenticate: Bearer authorization_uri=… resource_id=https://storage.azure.com`. | BL-424 |
| AzureStorage:BearerChallenge-2 | MUST | A bearer token that is no account is `401 InvalidAuthenticationInfo` with the same challenge. | BL-424 |
| AzureStorage:AnonymousRead-1 | MUST | Under `--allow-anonymous`, unauthenticated reads are served and unauthenticated writes are `401 NoAuthenticationInformation`. | BL-424 |
| AzureStorage:Surl-Auth-1 | MUST | Credentials sent under `--allow-anonymous` are checked; a bad one is never served as anonymous. | BL-424 |
| AzureStorage:Surl-Auth-2 | MUST | A write needs `--allow-uploads`; without it an authenticated write is `403 AuthorizationPermissionMismatch`. | BL-425 |
| AzureStorage:Surl-Auth-3 | MUST | Another `Authorization` scheme, or one that does not parse, is `400 InvalidAuthenticationInfo`. | BL-424 |
| AzureStorage:Surl-Auth-4 | MUST | The checked account is what the store's share access check receives. | BL-425 |
| AzureStorage:AuthorizeSharedKey-1 | MUST | `Authorization: SharedKey <account>:<signature>` over the page's Blob and Data Lake string to sign is verified with the account key. | BL-717 |
| AzureStorage:AuthorizeSharedKey-2 | MUST | `SharedKeyLite` over the page's shorter string to sign is verified. | BL-717 |
| AzureStorage:AuthorizeSharedKey-3 | MUST | A signature that does not verify, an unknown account, or a date missing or more than 15 minutes off is `403 AuthenticationFailed`. | BL-717 |
| AzureStorage:AuthorizeSharedKey-4 | MUST | On the path-style form the canonicalized resource carries the account twice. | BL-717 |
| AzureStorage:ServiceSas-1 | MUST | A service SAS is verified with the account named by the `Host` or the path-style segment. | BL-717 |
| AzureStorage:AccountSas-1 | MUST | An account SAS is verified the same way, with `ss` and `srt` checked. | BL-717 |
| AzureStorage:ServiceSas-2 | MUST | An expired or not-yet-valid SAS, a wrong `sip` or `spr`, or a stored access policy is `403 AuthenticationFailed`. | BL-717 |
| AzureStorage:ServiceSas-3 | MUST | An operation `sp` does not grant is `403 AuthorizationPermissionMismatch`. | BL-717 |
| AzureStorage:UserDelegationSas-1 | MAY | A user delegation SAS is verified with a key from Get User Delegation Key. | n/a: `403 AuthenticationFailed`; Surl's bearer tokens carry no Entra identity (ADR-0109 decision 3.3) |
| AzureStorage:Surl-Auth-5 | MUST | A SAS with an `Authorization` header as well is `400 InvalidAuthenticationInfo`. | BL-717 |

## TLS and limits (ADR-0109 decisions 6 and 7; ADR-0006)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| AzureStorage:Surl-Tls-1 | MUST | `az` and `abfss` are implicit TLS, `--cert` or `--self-signed` required (58 without), ALPN `http/1.1` only. | BL-427 |
| AzureStorage:Surl-Tls-2 | MUST | `abfs` is plain HTTP/1.1. | BL-427 |
| AzureStorage:Surl-Tls-3 | MUST | `--az-plaintext` serves `az` as plain HTTP/1.1 and is warned on start. | BL-427 |
| AzureStorage:AuthorizeEntra-3 | MUST | A bearer token over plain text is `401 InvalidAuthenticationInfo` unless `--allow-plaintext-auth`. | BL-427 |
| AzureStorage:Surl-Limits-1 | MUST | A head past `--max-request-head` is `400 InvalidInput`, then close. | BL-424 |
| AzureStorage:Surl-Limits-2 | MUST | A body, block, append or committed blob past `--max-filesize` is `413 RequestBodyTooLarge`, then close. | BL-425 |
| AzureStorage:Surl-Limits-3 | MUST | Past `--max-connections` or `--max-connections-per-address` is `503 ServerBusy` in the endpoint's body, then close. | BL-424 |
| AzureStorage:Surl-Limits-4 | MUST | `--head-timeout`, `--idle-timeout` and `--max-time` close with no bytes. | BL-424 |
| AzureStorage:Surl-Limits-5 | MUST | Metadata past 8 KiB is `400 MetadataTooLarge`; a name that is not a C# identifier `400 InvalidMetadata`; a full metadata index `503 ServerBusy`. | BL-425 |
