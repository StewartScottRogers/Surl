# ADR-0109 — How the Azure Storage server answers `az`, `abfs` and `abfss`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-423. BL-424 builds the server with Get Blob, Get Blob Properties, the required headers, the
  bearer and anonymous logins and the error bodies (decisions 1 to 5), BL-425 Put Blob, List Blobs
  and Delete Blob (decisions 1 and 3), BL-426 the Data Lake path requests and Path - Delete
  (decisions 1, 2 and 5), BL-717 Shared Key and shared access signatures (decision 3), BL-715 Put
  Block, Put Block List and Get Block List (decisions 1 and 7), BL-716 List Containers and the Data
  Lake rename, properties and filesystem requests (decision 1), and BL-427 serves the three schemes
  from `surl` with their TLS (decision 6).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; decision 7's ports),
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decisions 1, 4 and 5 (`Surl.HttpServing`, logins through `IHttpAuthenticationSession`, objects on
  `Surl.Content`), [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decisions 2 and 4.1 (the container mapping, refused names, the object metadata index, the shared
  accounts), [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decisions 2.2 to 2.4 (share access and `--bucket`), [ADR-0108](ADR-0108-how-the-s3-server-answers-s3.md)
  (the sibling object server; its refusal reason in `HttpAuthenticationVerdict` is reused),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) (secure by
  default) and [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits, fixed error text).
- **Amends:** nothing. ADR-0102 decision 2.3's planning row for AzureStorage (`404 BlobNotFound` /
  `ContainerNotFound`, `403 AuthorizationPermissionMismatch`) is kept as it stands.

## Context

`Surl.Extended.Protocol.AzureStorage` (in `Protocols/FileTransfer/`, ADR-0083's table) answers the
`az`, `abfs` and `abfss` schemes the Curl port adds: `az` is the Azure Blob service, `abfs` and
`abfss` the Data Lake Storage Gen2 ("DFS") endpoint, without and with TLS. By ADR-0083 decision 1 it
is judged by Microsoft's Azure Storage REST API reference at `x-ms-version: 2025-01-05`, not by
upstream curl, which has none of these schemes, and not by the Curl port. The pages read
(2026-11-05) and relied on, cited below by title, all under
<https://learn.microsoft.com/rest/api/storageservices/>:

- **Blob service:** "Get Blob", "Get Blob Properties", "Put Blob", "Put Block", "Put Block List",
  "Get Block List", "List Blobs", "Delete Blob", "List Containers", "Get Container Properties",
  "Specifying the range header for Blob service operations", "Specifying conditional headers for
  Blob service operations", "Naming and Referencing Containers, Blobs, and Metadata", "Blob Storage
  error codes".
- **Data Lake Storage Gen2:** "Path - Create", "Path - Update", "Path - Read", "Path - List", "Path -
  Delete", "Path - Get Properties", "Filesystem - Get Properties", "Filesystem - List", and the
  `DataLakeStorageError` definition each of those pages carries.
- **Common:** "Common REST API error codes", "Versioning for the Azure Storage services", "Status
  and error codes", "Authorize with Microsoft Entra ID" (its "Bearer Challenge" section), "Authorize
  with Shared Key", "Create a service SAS", "Create an account SAS".

Which requests a real client sends, as background and not as a verdict: the port's
`Curl.Extended.Protocol.AzureStorage.UnitLibrary` sends, for `az`, Get Blob, Put Blob (and Put Block
then Put Block List from 8 MiB) and List Blobs with `delimiter=%2F`, `prefix` and `marker`; for
`abfs`/`abfss`, Path - Read, Path - Create `resource=file`, Path - Update `append` at each position
then `flush`, and Path - List with `recursive=false`. Every request carries `Host`
(`<account>.blob.core.windows.net` or `<account>.dfs.core.windows.net`, or the URL's host when it
holds a dot), `x-ms-version: 2025-01-05` and `x-ms-date`; it authorizes with `Authorization: Bearer`,
a SAS token in the query, or nothing. It does not sign Shared Key. Microsoft's own SDKs and Hadoop's
ABFS driver also send Shared Key, rename, and Get Properties, so the server answers those too.

## Decision

### 1. The operations served, and the answer for any other

The **listen scheme decides the endpoint**: a server listening on `az` is a Blob endpoint, one on
`abfs` or `abfss` a Data Lake endpoint; the `Host` field never switches between them. Both serve the
same content store, so a file written over `abfss` is a blob over `az`, and the reverse.

An operation is recognised by method, by whether the path names a container and a blob, and by its
query parameters, as each page's "Request" section writes it.

**Blob endpoint (`az`):**

| Operation | Request | Success | Task |
| --- | --- | --- | --- |
| List Containers | `GET /?comp=list` | `200`, `EnumerationResults` | BL-716 |
| Get Container Properties | `GET`/`HEAD /<container>?restype=container` | `200`, no body | BL-716 |
| List Blobs | `GET /<container>?restype=container&comp=list` | `200`, `EnumerationResults` (decision 5.3) | BL-425 |
| Get Blob | `GET /<container>/<blob>` | `200`, or `206` for a range | BL-424 |
| Get Blob Properties | `HEAD /<container>/<blob>` | `200`, Get Blob's head, no body | BL-424 |
| Put Blob | `PUT /<container>/<blob>` with `x-ms-blob-type: BlockBlob` | `201`, no body | BL-425 |
| Delete Blob | `DELETE /<container>/<blob>` | `202`, no body | BL-425 |
| Put Block | `PUT /<container>/<blob>?comp=block&blockid=<id>` | `201` | BL-715 |
| Put Block List | `PUT /<container>/<blob>?comp=blocklist` | `201` | BL-715 |
| Get Block List | `GET /<container>/<blob>?comp=blocklist` | `200`, `BlockList` | BL-715 |

**Data Lake endpoint (`abfs`, `abfss`):**

| Operation | Request | Success | Task |
| --- | --- | --- | --- |
| Filesystem - List | `GET /?resource=account` | `200`, `{"filesystems":[…]}` | BL-716 |
| Filesystem - Get Properties | `HEAD /<filesystem>?resource=filesystem` | `200`, no body | BL-716 |
| Path - List | `GET /<filesystem>?resource=filesystem&recursive=<bool>` | `200`, `{"paths":[…]}` (decision 5.4) | BL-426 |
| Path - Read | `GET /<filesystem>/<path>` | `200`, or `206` for a range | BL-426 |
| Path - Get Properties | `HEAD /<filesystem>/<path>` | `200`, no body, `x-ms-resource-type` | BL-716 |
| Path - Create | `PUT /<filesystem>/<path>?resource=file\|directory` | `201`, no body | BL-426 |
| Path - Create (rename) | `PUT /<filesystem>/<path>` with `x-ms-rename-source` | `201` | BL-716 |
| Path - Update | `PATCH /<filesystem>/<path>?action=append&position=<n>` | `202` | BL-426 |
| Path - Update | `PATCH …?action=flush&position=<n>` | `200` | BL-426 |
| Path - Update | `PATCH …?action=setProperties` | `200` | BL-716 |
| Path - Delete | `DELETE /<filesystem>/<path>` (`recursive` for a directory) | `200` | BL-426 |

- **Any other request is refused with the code the "Common REST API error codes" page gives the
  cause**, judged after authentication (decision 3) so an unauthenticated peer learns only the
  authentication answer:
  - a method the resource does not take: **`405 UnsupportedHttpVerb`**;
  - an unknown `comp`, `restype`, `resource` or `action` value (`comp=metadata`, `comp=lease`,
    `comp=snapshot`, `comp=tags`, `comp=appendblock`, `comp=page`, `restype=container` with `PUT` or
    `DELETE`, `action=setAccessControl`, `action=setAccessControlRecursive`, `action=getAccessControl`,
    …): **`400 InvalidQueryParameterValue`**, with `QueryParameterName`, `QueryParameterValue`
    and `Reason`;
  - a header asking for a feature the server does not have - `x-ms-copy-source` (Copy Blob),
    `x-ms-blob-type: AppendBlob` or `PageBlob`, `x-ms-lease-id`, `x-ms-lease-action`,
    `x-ms-proposed-lease-id`, `x-ms-encryption-key`, `x-ms-encryption-scope`, `x-ms-acl`,
    `x-ms-permissions`, `x-ms-owner`, `x-ms-group`, `x-ms-umask`, `x-ms-structured-body`,
    `x-ms-expiry-option`: **`400 UnsupportedHeader`**, with `HeaderName`.
- **Containers and filesystems are not created or deleted** (Create Container, Delete Container,
  Filesystem - Create, Filesystem - Delete are the unknown-`restype`/`resource` row above): a
  container is a top-level folder every file server shares, made by `--directory`'s contents, by
  another protocol or by `--bucket`, as ADR-0108 decision 1 decides for S3 buckets.
- **Leases, snapshots, versions, tags, append and page blobs, access control lists and customer
  keys are not served.** Each needs state the shared file store does not hold (a lease clock per
  file, a second copy per snapshot, a POSIX owner per path), and a lease or ACL a peer sets would bind
  only the Azure server while FTP, SFTP and SMB write past it. Refusing them names exactly what is
  missing; serving a lease that other protocols ignore would be a false promise.
- **Ranges** ("Specifying the range header"): one `Range: bytes=` or `x-ms-range: bytes=` range is
  `206` with `Content-Range`; `x-ms-range` wins when both are sent; an unsatisfiable one is **`416
  InvalidRange`**; several ranges, or a syntax the page does not give, are ignored and the whole
  blob served `200`. `x-ms-range-get-content-md5: true` adds the range's `Content-MD5` for ranges up
  to 4 MiB and is `400 OutOfRangeInput` past it.
- **Conditional headers** ("Specifying conditional headers"): on a read, `If-Match` or
  `If-Unmodified-Since` failing is `412 ConditionNotMet`, `If-None-Match` matching or
  `If-Modified-Since` not modified is `304` with no body; on a write, any failing condition is
  **`412 ConditionNotMet`**, and `If-None-Match: *` on an existing blob is **`409 BlobAlreadyExists`**
  (Blob) or **`409 PathAlreadyExists`** (Data Lake).
- **Response headers on every answer:** `x-ms-request-id` (a lower-case GUID from
  `RandomNumberGenerator`, the same value as the error body's `RequestId`), `x-ms-version:
  2025-01-05`, and `Date`, which `Surl.HttpServing` writes. `x-ms-client-request-id`, when sent and
  at most 1024 visible ASCII bytes, is echoed, as every operation page lists it. Get Blob and Get Blob
  Properties also carry `Content-Length`, `Content-Type`, `Content-MD5` (the base64 of the index's
  MD5), `ETag`, `Last-Modified` (IMF-fixdate, the file's last-write time), `Accept-Ranges: bytes`,
  `x-ms-blob-type: BlockBlob`, `x-ms-lease-status: unlocked`, `x-ms-lease-state: available`,
  `x-ms-server-encrypted: false`, each stored `Content-Encoding`, `Content-Language`,
  `Content-Disposition` and `Cache-Control`, and each stored `x-ms-meta-*`. Path - Read and Path -
  Get Properties carry the same entity fields with `x-ms-resource-type: file` or `directory` in
  place of the blob fields. A file another protocol wrote is served with the index's defaults
  (ADR-0100 decision 2.3).

### 2. Hosts, paths and the account

- **The account is never part of the store.** Surl serves one content store, so every account name
  addresses the same containers; like ADR-0108's "every region at once", the server is every
  account at once. The account name in a `Host` (its first label) is read only to name the account a
  shared access signature was signed by (decision 3.3); Shared Key compares the signature's account
  with the account set (decision 3.2), never with the `Host`.
- **Account-host form, the default:** the path is `/<container>/<blob>` (Blob) or
  `/<filesystem>/<path>` (Data Lake), whatever the `Host` says - `<account>.blob.core.windows.net`,
  `<account>.dfs.core.windows.net`, any other cloud's suffix, the listen host, or none. `/` alone is
  the account (List Containers, Filesystem - List). This is the form the "Get Blob" and Data Lake
  pages' URIs give, `https://{accountName}.{dnsSuffix}/{filesystem}/{path}`.
- **Path-style form** ("Naming and Referencing Containers, Blobs, and Metadata": emulator and
  IP-style addressing `http://<ip>:<port>/<account>/<container>/<blob>`): when the `Host` name (port
  removed) is an IP literal or `localhost`, and the path's first segment is the name of a non-empty
  account in the account set, that segment is the account and is removed. A container with the
  same name as an account cannot then be reached through an IP-literal or `localhost` host; it can
  through any other host name. With no named accounts (anonymous or bearer only) the path-style
  form never applies.
- **Names on the store** apply ADR-0100 decision 2: a container or filesystem is a top-level folder
  of the one shared `ContentStore`, resolved through `IContentShareLookup` so `--bucket` needs no
  Azure change; `.surl` and a top-level file are never containers; a container name is not refused
  for its characters (ADR-0100 decision 2.1). A blob name or path `x/y/z` is file `z` in folder
  `x/y`; it is percent-decoded once from the path, as UTF-8. A blob name longer than 1024 characters
  ("Naming and Referencing": "A blob name can contain any combination of characters and must be at
  least one character long and cannot be more than 1,024 characters") is **`400
  InvalidResourceName`** on every operation.
- **Names the store cannot hold** (ADR-0100 decision 2.2's list): a write is **`400
  InvalidResourceName`**; a name whose segment would shadow an entry of the other kind is **`409
  InvalidResourceName`** (Blob) or **`409 PathConflict`** (Data Lake, the code its pages give that
  case); a read of either is `404 BlobNotFound` or `404 PathNotFound`.
- **A folder is not a blob.** Get Blob of a folder is `404 BlobNotFound`; Path - Read of a directory
  is **`409 ResourceTypeMismatch`** ("The resource type specified in the request does not match the
  type of the resource"), because on the Data Lake endpoint a directory exists. A zero-byte Put Blob
  whose name ends in `/` creates the folder (ADR-0100 decision 2.1); a non-empty one is `400
  InvalidResourceName`. Path - Create `resource=file` and `resource=directory` create every missing
  parent folder, as the hierarchical namespace does.
- **Share access** (ADR-0102 decision 2.3): a container or blob the caller may not read is `404
  ContainerNotFound` / `BlobNotFound` (Blob) or `404 FilesystemNotFound` / `PathNotFound` (Data
  Lake); a write the caller may not make is **`403 AuthorizationPermissionMismatch`**, Message `This
  request is not authorized to perform this operation using this permission.` (the
  `StorageErrorCode` value Azure sends for a permission the caller lacks; ADR-0102's planning row).
  Every store call names the caller (the checked account, or anonymous) and the scheme (`az`,
  `abfs` or `abfss`).

### 3. Authentication

#### 3.1 What is required

- **By default every request carries a valid login** of the one account set (`--user`,
  `--user-file`; ADR-0100 decision 4.1): `Authorization: Bearer` (BL-424), `Authorization:
  SharedKey` or `SharedKeyLite`, or a shared access signature in the query (BL-717).
- **Bearer tokens** ("Authorize with Microsoft Entra ID") are checked by `Surl.Authentication`'s
  existing `BearerAuthenticationMethod` against the empty-name accounts' passwords, in constant time
  (ADR-0032 section 1). A token is opaque: no JSON Web Token is parsed, no tenant, audience or
  expiry read, because Surl issues no Entra tokens and a harness hands its client whatever string it
  configured. A bearer request needs `x-ms-version` 2017-11-09 or later (the page: "specify a
  service version of 2017-11-09 … or higher"); an earlier one is `403 AuthenticationFailed`.
- **No login with anonymous off, or a bearer token that is no account:** **`401
  NoAuthenticationInformation`** (no `Authorization`) or **`401 InvalidAuthenticationInfo`** (a bearer
  token that does not verify), each with the page's challenge, `WWW-Authenticate: Bearer
  authorization_uri=https://login.microsoftonline.com/common/oauth2/authorize
  resource_id=https://storage.azure.com` ("Bearer Challenge": the Blob service sends it from
  2019-12-12, the Data Lake service from 2017-11-09, "for anonymous requests … or for requests made
  with an invalid OAuth bearer token"). The challenge is sent whatever `x-ms-version` the request
  names: Surl answers as 2025-01-05 (decision 4), a version that challenges, so the page's
  pre-2019-12-12 `404`/`409` rows never apply. `common` stands for the tenant: Surl has none, and
  `common` is Entra's multi-tenant authority.
- **`--allow-anonymous`** serves unauthenticated reads (List Containers, Get Container Properties,
  List Blobs, Get Blob, Get Blob Properties, Get Block List, and the Data Lake reads and lists), as
  the page's anonymous-read conditions do for a public container. An anonymous write is `401
  NoAuthenticationInformation` with the challenge. A request that carries credentials is checked
  even then; a bad one is never served as anonymous.
- **Writes** need `--allow-uploads` (ADR-0006 section 2) **and** a login; with uploads off, an
  authenticated write is `403 AuthorizationPermissionMismatch` (ADR-0102 decision 2.3's
  `NotPermitted`).
- **Order:** request head and limits, then the required headers (decision 4), then authentication,
  then the operation, the container, the share access and the name.
- **Each refusal waits the failed-login delay** the authentication session already applies
  (ADR-0032), then answers; the connection stays open.

#### 3.2 Shared Key (BL-717)

"Authorize with Shared Key": `Authorization: SharedKey <account>:<signature>` and `SharedKeyLite
<account>:<signature>`, the HMAC-SHA256 under the base64-decoded account key of the page's string to
sign - for the Blob and Data Lake services the verb, the twelve standard headers (`Content-Length`
empty when zero), the canonicalized `x-ms-` headers and the canonicalized resource
`/<account>/<path>` with each query parameter on its own line - and for `SharedKeyLite` the page's
shorter form. The account name is an account in the set and the password its key's base64 text
(ADR-0100 decision 4.1). `x-ms-date` (or `Date` when `x-ms-date` is absent) is required and must be
within 15 minutes of the injected `TimeProvider` either way. On the path-style form the
canonicalized resource carries the account twice, `/<account>/<account>/<container>/<blob>`, as the
page says for emulator addressing. Shared Key sends no secret, so it is served over plain `az` and
`abfs` without `--allow-plaintext-auth`.

#### 3.3 Shared access signatures (BL-717)

- **Service SAS and account SAS are served** ("Create a service SAS", "Create an account SAS"): the
  query's `sv`, `ss`, `srt`, `sr`, `sp`, `st`, `se`, `sip`, `spr`, `si`, `sig` (and the response
  override fields) are read per the page's string to sign for `sv` 2020-12-06 and later. The
  canonicalized resource names the account, which the query does not carry, so the server takes it
  from the request: the `Host`'s first label in the account-host form, the removed first segment in
  the path-style form (decision 2). The signature is verified with that account's key; a name that
  is no account in the set is `403 AuthenticationFailed`. `se` past or `st` ahead of the clock (15
  minutes' skew allowed, as Shared Key), `sip` not matching the peer, or `spr=https` over plain text
  is `403 AuthenticationFailed`; an operation `sp` (or `srt`/`ss`) does not grant is **`403
  AuthorizationPermissionMismatch`**. A stored access policy (`si`) is `403 AuthenticationFailed`:
  there is no Set Container ACL to store one.
- **A user delegation SAS** (`skoid` and its `sk*` fields) is **`403 AuthenticationFailed`**: it is
  signed with a key only Get User Delegation Key issues, from an Entra identity's object ID, and
  Surl's opaque bearer tokens carry no identity to derive one from.
- A SAS with an `Authorization` header as well is `400 InvalidAuthenticationInfo`.

#### 3.4 Each refusal

| Failure | Status, code |
| --- | --- |
| No credentials, anonymous off; or an anonymous write | `401 NoAuthenticationInformation` + challenge |
| Bearer token that is no account | `401 InvalidAuthenticationInfo` + challenge |
| `Authorization` with another scheme (Basic, Digest, `AWS4-HMAC-SHA256`), or a SharedKey header that does not parse | `400 InvalidAuthenticationInfo`, Message `Authentication information is not given in the correct format. Check the value of Authorization header.` |
| Bearer with `x-ms-version` before 2017-11-09 | `403 AuthenticationFailed` |
| Shared Key or SAS signature that does not verify, or an account that is no account | `403 AuthenticationFailed` |
| Shared Key date missing or more than 15 minutes off; SAS expired, not yet valid, wrong `sip` or `spr`; a stored policy; a user delegation SAS | `403 AuthenticationFailed` |
| A permission the SAS does not grant; a write with uploads off or share access refusing it | `403 AuthorizationPermissionMismatch` |

- **An unknown account is `AuthenticationFailed`, as a bad signature is.** Unlike S3's access key
  ID (ADR-0108 decision 4.3), Azure has no distinct code for an unknown account; the check runs
  against a random key so both cases take the same time.
- **`AuthenticationErrorDetail`**, the element Azure adds to `AuthenticationFailed`, is a fixed
  sentence per cause (`Signature did not match.`, `Request date header too old.`, `Signed expiry
  time has passed.`, …), **never** the string to sign the server computed, which would tell a
  probing peer how the request was canonicalised (ADR-0108's reason for dropping `StringToSign`).
- **How this differs from the HTTP server.** The HTTP server challenges with Digest first (ADR-0032
  section 4). The Azure server's only challenge is the Bearer one above, which the page fixes, and
  `--auth` does not apply to the three schemes. BL-717 reuses BL-420's refusal reason on
  `HttpAuthenticationVerdict` (ADR-0108 decision 4.4) to tell these rows apart; the server
  references neither `Surl.Authentication` nor an account (ADR-0084 decision 4).

### 4. The required headers

- **`x-ms-version`** ("Versioning for the Azure Storage services") is **required on every
  authenticated request** - Bearer, Shared Key, and a SAS whose `sv` stands for it - and optional on
  an anonymous one. Missing where required: **`400 MissingRequiredHeader`**, `HeaderName`
  `x-ms-version`. A value that is not a `yyyy-MM-dd` date is **`400 InvalidHeaderValue`**, `HeaderName`
  `x-ms-version`. Any well-formed version is accepted and answered with 2025-01-05's bytes, and
  `x-ms-version: 2025-01-05` names that version in every answer: Surl implements one version, and
  says which.
- **`x-ms-date`** (or `Date`) is required for Shared Key (decision 3.2; missing is `403
  AuthenticationFailed`, as the Shared Key page's "valid date header" rule fails) and not required
  otherwise. Sent and not an RFC 1123 time in GMT: **`400 InvalidHeaderValue`**, `HeaderName`
  `x-ms-date`.
- **`x-ms-blob-type`** is required on Put Blob: missing is `400 MissingRequiredHeader`,
  `HeaderName` `x-ms-blob-type`; `AppendBlob` or `PageBlob` `400 UnsupportedHeader`; another value
  `400 InvalidHeaderValue`.
- **`Content-Length`** is required on Put Blob, Put Block, Put Block List and Path - Update:
  missing (and no chunked body) is **`411 MissingContentLengthHeader`**; on `action=flush` it must
  be 0, else **`400 ContentLengthMustBeZero`**.
- **Query parameters:** `position` on `append` and `flush`, `recursive` on Path - List and
  `blockid` on Put Block are required (**`400 MissingRequiredQueryParameter`**,
  `QueryParameterName`); a value that does not parse is `400 InvalidQueryParameterValue`.

### 5. The error bodies, and the listings

#### 5.1 Blob endpoint: XML ("Status and error codes")

```
<?xml version="1.0" encoding="utf-8"?><Error><Code>BlobNotFound</Code><Message>The specified blob does not exist.
RequestId:5f0e8a3c-1d2b-4c6e-9f70-a1b2c3d4e5f6
Time:2026-11-05T09:12:44.1234567Z</Message></Error>
```

- `Content-Type: application/xml`, UTF-8, one line between elements; **`x-ms-error-code: <Code>`**
  on every error, as the "Status and error codes" page says the service returns it.
- `Code` and the first line of `Message` are **fixed per code**, the "User message" column of the
  "Common REST API error codes" and "Blob Storage error codes" tables (ADR-0006 section 3); the
  `RequestId:` and `Time:` lines follow, as the service writes them, from the server's own request ID
  and clock.
- The extra elements the pages name are sent: `HeaderName` (with `HeaderValue` for
  `InvalidHeaderValue`), `QueryParameterName`, `QueryParameterValue` and `Reason`, and
  `AuthenticationErrorDetail` (fixed, decision 3.4). Client bytes appear only in `HeaderValue` and
  `QueryParameterValue`, XML-escaped and cut to 1024 bytes.
- A `HEAD` error (Get Blob Properties, Get Container Properties) has its status, `x-ms-error-code`,
  `x-ms-request-id` and no body.

#### 5.2 Data Lake endpoint: JSON (`DataLakeStorageError`)

```
{"error":{"code":"PathNotFound","message":"The specified path does not exist.\nRequestId:5f0e8a3c-1d2b-4c6e-9f70-a1b2c3d4e5f6\nTime:2026-11-05T09:12:44.1234567Z"}}
```

- `Content-Type: application/json;charset=utf-8`, `x-ms-error-code: <code>`; the codes and
  messages are those the Data Lake pages list ("400 Bad Request, InvalidFlushPosition, 'The uploaded
  data is not contiguous …'"), which differ in wording from the Blob tables for the same code - each
  endpoint uses its own pages' text. `code` and `message` are the only members; no client byte is
  echoed. A `HEAD` error has no body.

The codes the server sends, each with the status its page gives it:

| Code | Status | Endpoint | When |
| --- | --- | --- | --- |
| `NoAuthenticationInformation`, `InvalidAuthenticationInfo` | 401 (and 400 for a malformed header) | both | decision 3.4 |
| `AuthenticationFailed`, `AuthorizationPermissionMismatch` | 403 | both | decision 3.4 |
| `MissingRequiredHeader`, `InvalidHeaderValue`, `UnsupportedHeader` | 400 | both | decisions 1 and 4 |
| `MissingRequiredQueryParameter`, `InvalidQueryParameterValue`, `OutOfRangeQueryParameterValue` | 400 | both | decisions 1, 4 and 5.3 |
| `UnsupportedHttpVerb` | 405 | both | decision 1 |
| `MissingContentLengthHeader` | 411 | both | decision 4 |
| `ContentLengthMustBeZero`, `InvalidFlushPosition` | 400 | Data Lake | decisions 4 and 5.5 |
| `Md5Mismatch`, `InvalidMd5` | 400 | both | a `Content-MD5` not matching the body, or not base64 of 16 bytes |
| `InvalidResourceName` | 400, 409 | Blob (and Data Lake 400) | decision 2 |
| `PathConflict`, `ResourceTypeMismatch`, `PathAlreadyExists` | 409 | Data Lake | decisions 1, 2 and 5.5 |
| `BlobAlreadyExists` | 409 | Blob | `If-None-Match: *` on an existing blob |
| `ConditionNotMet` | 412 (304 on a read) | both | decision 1 |
| `InvalidRange` | 416 | both | decision 1 |
| `ContainerNotFound`, `BlobNotFound` | 404 | Blob | decision 2 |
| `FilesystemNotFound`, `PathNotFound` | 404 | Data Lake | decision 2; Path - List's `directory` that does not exist |
| `DirectoryNotEmpty` | 409 | Data Lake | Path - Delete of a non-empty directory without `recursive=true` |
| `InvalidBlockId`, `InvalidBlockList`, `BlockCountExceedsLimit`, `BlockListTooLong` | 400, 400, 409, 400 | Blob | BL-715 |
| `MetadataTooLarge`, `InvalidMetadata` | 400 | both | decision 7 |
| `RequestBodyTooLarge` | 413 | both | decision 7 |
| `ServerBusy` | 503 | both | decision 7 |

#### 5.3 List Blobs ("List Blobs")

- **Body:** `EnumerationResults` with `ServiceEndpoint` (`https://` or `http://` as served, the
  `Host` field, `/`) and `ContainerName`; then `Prefix`, `Marker`, `MaxResults` and `Delimiter` when
  sent; `Blobs` holding each `Blob` (`Name`; `Properties` with `Creation-Time` equal to
  `Last-Modified` when the store keeps no creation time, `Last-Modified`, `Etag`, `Content-Length`,
  `Content-Type`, `Content-Encoding`, `Content-Language`, `Content-MD5`, `Cache-Control`,
  `Content-Disposition`, `BlobType` `BlockBlob`, `AccessTier` `Hot`, `AccessTierInferred` `true`,
  `LeaseStatus` `unlocked`, `LeaseState` `available`, `ServerEncrypted` `false`, as the page's
  example orders them; `Metadata` only with `include=metadata`) and each `BlobPrefix` `Name`; then
  `NextMarker`, empty on the last page.
- **Order:** names in ascending UTF-8 byte order, the order ADR-0108 decision 6 builds by walking
  sorted folders.
- **`maxresults`**: default 5000, above 5000 served as 5000, 0 or below `400
  OutOfRangeQueryParameterValue`; it bounds blobs and prefixes together. **`prefix`** limits;
  **`delimiter`** (any string) rolls names up to the first delimiter after `prefix` into one
  `BlobPrefix`. **`marker`** is opaque: the base64url (no padding) of the UTF-8 bytes of the last
  name or prefix returned; one that does not decode is `400 InvalidQueryParameterValue`.
- **`include`**: `metadata` is served; `uncommittedblobs` lists blobs staged by Put Block and not
  committed (BL-715); `snapshots`, `versions`, `deleted`, `tags`, `copy`, `immutabilitypolicy`,
  `legalhold` and `permissions` are accepted and add nothing, because none of those exist on Surl;
  another value is `400 InvalidQueryParameterValue`.

#### 5.4 Path - List ("Path - List")

- **Body:** `{"paths":[…]}`, each entry with `name` (the path from the filesystem root), `isDirectory`
  `true` for a directory and absent for a file (the page's default `False`), `contentLength` and
  `lastModified` (IMF-fixdate), `etag`, `owner` and `group` `$superuser`, `permissions` `rwxr-x---`
  for a directory and `rw-r-----` for a file. Types are the page's: `contentLength` an integer,
  `isDirectory` a boolean.
- `recursive` is required (decision 4); `false` lists one level of `directory` (or the root).
  `directory` names the folder to list; one that does not exist is `404 PathNotFound`. `maxResults`
  is 1 to 5000, default and cap 5000; 0 or below is `400 OutOfRangeQueryParameterValue`.
  `continuation` is the `x-ms-continuation` header of the previous page, opaque as `marker` is, and
  the header is absent on the last page. `beginFrom` starts after that relative path. `upn` is
  accepted and changes nothing: owners are `$superuser`.

#### 5.5 Data Lake writes ("Path - Create", "Path - Update")

- **Path - Create `resource=file`** creates the file empty, or truncates an existing one (the page:
  "By default, the destination is overwritten"), visibly, through the content store's upload
  session; `x-ms-content-type`, `x-ms-content-encoding`, `x-ms-content-language`,
  `x-ms-content-disposition`, `x-ms-cache-control` and `x-ms-properties` go to the metadata index.
- **`action=append&position=<n>`** stages the body at `n` under ADR-0100's
  `.surl/objects/uploads/<upload id>/`, one staging per path, invisible to every protocol; `202`.
  `flush=true` on an append appends and then flushes to `n` plus the body's length.
- **`action=flush&position=<n>`** commits: the staged data must cover the committed length to `n`
  without a gap or overlap, else **`400 InvalidFlushPosition`**; the file becomes its committed bytes
  plus the staged bytes up to `n`, written whole through the upload session; staged data past `n` is
  dropped unless `retainUncommittedData=true`. `200` with `ETag`, `Last-Modified` and
  `Content-Length: 0`. `close` is accepted and changes nothing (no change notifications).
- **An append or flush on a path with no file** is `404 PathNotFound`; on a directory `409
  ResourceTypeMismatch`.
- **Path - Delete** removes a file, or a directory: an empty one always, a non-empty one only with
  `recursive=true` (else `409 DirectoryNotEmpty`); `200`. A missing path is `404 PathNotFound`.

#### 5.6 Blob writes ("Put Blob", "Delete Blob")

- **Put Blob** stores the body, overwriting an existing blob, through the content store's upload
  session; `201` with `ETag`, `Last-Modified`, `Content-MD5` (base64 of the body's MD5) and
  `x-ms-request-server-encrypted: false`, no body. `Content-MD5`, when sent, is checked: not base64
  of 16 bytes is `400 InvalidMd5`, not the body's `400 Md5Mismatch`, and nothing is stored.
  `x-ms-blob-content-type` (else `Content-Type`), `x-ms-blob-content-encoding`,
  `x-ms-blob-content-language`, `x-ms-blob-content-disposition`, `x-ms-blob-cache-control` and each
  `x-ms-meta-*` (names lower-cased) go to ADR-0100 decision 2.3's metadata index and come back on Get
  Blob and Get Blob Properties. `x-ms-blob-content-md5`, when sent, is stored as the blob's
  `Content-MD5` without being checked, as the page says.
- **Delete Blob** removes the file; `202`. A missing blob is `404 BlobNotFound` (unlike S3, the page
  gives no success for a missing blob). `x-ms-delete-snapshots` is accepted and changes nothing:
  there are no snapshots.

### 6. TLS and plain text

- **`az` and `abfss` are HTTPS**: implicit TLS from the first byte, wrapped in
  `ImplicitTlsSchemeServer` as `https` is (ADR-0010), on port 443 (ADR-0083 decision 7); TLS 1.2 and
  1.3 (ADR-0006 section 4); `--cert` or `--self-signed` required, `CertificateProblem` (58) without,
  as ADR-0032 decides for every secure listen URL; ALPN offers `http/1.1` only.
- **`abfs` is plain HTTP/1.1** on port 80 (ADR-0083 decision 7): the scheme is Data Lake without
  TLS by definition, so no option is needed to choose it.
- **`--az-plaintext`** (no argument, category `azure-storage`, off by default, warned on start as
  ADR-0032's loosening options are) serves `az` as plain HTTP/1.1, for clients pointed at an
  emulator-style `http://` endpoint, as `--s3-plaintext` does for `s3` (ADR-0108 decision 5).
- **A bearer token over plain text** - on `abfs`, or on `az` under `--az-plaintext` - is refused with
  **`401 InvalidAuthenticationInfo`** and the challenge unless `--allow-plaintext-auth` is given
  (ADR-0032 section 3: a secret in the clear needs its own option); the page requires HTTPS for
  OAuth. Shared Key and a SAS send no secret and are served over plain text; a SAS with `spr=https`
  is not (decision 3.3).
- BL-427 adds `--az-plaintext` with its `OptionArgumentType`, help category and `--aihelp` facts.

### 7. Limits (ADR-0006)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| `--max-request-head` (100 KiB) | `Surl.HttpServing`'s head reader | `400 InvalidInput`, Message `One of the request inputs is not valid.`, then close |
| `--max-filesize` (100 MiB) | a Put Blob body, each Put Block and append body, the committed blob or flushed file; checked against `Content-Length` before the body is read, and again while it is written | `413 RequestBodyTooLarge`, then close (the body is not drained) |
| the largest blob | `--max-filesize`, however it is uploaded: an "uploaded file" in ADR-0006's words. Azure's own caps (5000 MiB a Put Blob, 4000 MiB a block, 50000 blocks, about 190.7 TiB a block blob, "Put Blob", "Put Block") are above any default and need no second bound | as above |
| blocks | 100000 uncommitted and 50000 committed blocks a blob, a block ID of at most 64 bytes before base64 ("Put Block"); 1000 blobs with staged blocks or appends per server, the next `503 ServerBusy`; staged data untouched for 7 days is dropped ("Put Block": an uncommitted block list is discarded after a week), timed on `TimeProvider` | `409 BlockCountExceedsLimit`, `400 BlockListTooLong`, `400 InvalidBlockId`; `503 ServerBusy` |
| a blob name | 1024 characters | `400 InvalidResourceName` |
| metadata | 8 KiB of `x-ms-meta-*` names and values ("Setting and retrieving properties and metadata", ADR-0100's per-object bound); names must be C# identifiers (`400 InvalidMetadata`); ADR-0100's 100000 index entries | `400 MetadataTooLarge`; `503 ServerBusy` |
| a Put Block List body | 50000 entries and 4 MiB | `400 BlockListTooLong`; `400 InvalidXmlDocument` past 4 MiB |
| a listing page | 5000 entries (decisions 5.3 and 5.4) | served as 5000 |
| `--head-timeout` (30 s), `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.HttpServing`'s and `Surl.Core`'s | close with no bytes |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s, through the server's refusal writer | `503 ServerBusy`, Message `The server is currently unable to receive requests. Please retry your request.`, in the endpoint's body, then close (ADR-0006 section 5: the protocol's own error) |

No other new option is added.

### 8. `--aihelp`

The topic is `azure-storage` (ADR-0083 decision 8 names it). Its about text says the server answers
the Azure Blob service on `az` and the Data Lake Storage Gen2 endpoint on `abfs` and `abfss` over the
shared content store, is judged by the Azure Storage REST API at 2025-01-05 and not by upstream curl,
requires a login by default (a bearer token from `--user ':<token>'`, Shared Key or a SAS), and is
HTTPS on `az` and `abfss` unless `--az-plaintext`. BL-427 writes it with its example.

## Alternatives considered

- **Choosing the endpoint from the `Host` (`.blob.` or `.dfs.`).** Rejected: a client pointed at
  Surl by IP literal or `localhost` has no such label, and the listen scheme already says which
  service the harness asked for.
- **Treating the account as a namespace** (one store folder per account). Rejected: ADR-0100 makes
  one content store shared by every protocol, which has no accounts in its paths; FTP's
  `/photos/a.jpg` must be blob `a.jpg` in container `photos` whatever account name the Azure client
  uses.
- **Validating bearer tokens as Entra JSON Web Tokens.** Rejected: Surl issues none and has no Entra
  signing keys to check one against; an opaque comparison against the account set is what a test
  harness can configure, and what ADR-0100 decision 4.1 decided.
- **Answering every unserved request with Azurite's `501 NotImplemented`.** Rejected: no Azure
  Storage page lists that code; `UnsupportedHttpVerb`, `InvalidQueryParameterValue` and
  `UnsupportedHeader` are the pages' own answers and tell the client what was not understood.
- **Serving leases, snapshots and ACLs against the Azure server alone.** Rejected (decision 1):
  they would bind one protocol while six others write past them.
- **Emitting Path - List's `contentLength` and `isDirectory` as strings**, as the live service is
  known to. Rejected: the page's schema types them integer and boolean, and the specification is
  the oracle (ADR-0083 decision 1).
- **Serving user delegation SAS.** Rejected (decision 3.3): it needs an Entra identity Surl's tokens
  do not carry.

## Consequences

Good:

- The Azure server answers every request the port's client sends - including the block uploads
  from 8 MiB (BL-715) - and the common SDK and Hadoop forms, on the same files, accounts and shares as
  FTP, SFTP, HTTP, SMB and S3.
- Every refusal is one fixed row with its page's code, status and message, so tests pin each byte
  from the REST API reference.

Costs and caveats:

- BL-717 changes `Surl.Authentication` and `Surl.Protocol.Abstractions`, so it runs apart from every
  protocol task, and waits for BL-420's refusal reason.
- A container named like a named account cannot be reached through an IP-literal or `localhost`
  host (decision 2).
- The matrix is `Documentation/Conformance/Extended/AzureStorage.md` (ADR-0083 decision 9), one file
  for both endpoints.
