# ADR-0110 — How the Google Cloud Storage server answers `gs`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-429. BL-431 builds the server with every operation, addressing, bearer and anonymous access,
  the error body and the limits (decisions 1, 2, 3.1, 3.2, 3.4, 6 and 7). BL-430 adds the
  `GOOG4-HMAC-SHA256` check to `Surl.Authentication` (decision 3.3). BL-432 wires that check into the
  server (decision 3.3's refusals). BL-433 serves `gs` from `surl` with its TLS, its option and its
  `--aihelp` topic (decisions 5 and 8).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof),
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decisions 1, 4 and 5 (`Surl.HttpServing`, logins through `IHttpAuthenticationSession`, objects on
  `Surl.Content`), [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decisions 2 and 4.1 (the bucket mapping, refused names, the object metadata index, the shared
  accounts), [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decisions 2.2 to 2.4 (share access and `--bucket`), [ADR-0043](ADR-0043-http-aws-signature-version-4-check.md)
  and [ADR-0045](ADR-0045-an-aws-signature-version-4-body-is-bound-after-it-is-read.md) (the SigV4
  check that V4 HMAC shares), [ADR-0108](ADR-0108-how-the-s3-server-answers-s3.md) (the S3 server,
  whose shape this one follows where the two APIs agree), [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  (secure by default) and [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits, fixed
  error text).
- **Amends:** ADR-0100 decision 2.2's planning row for GoogleCloudStorage. That row named a JSON API
  error with reason `invalid` or `conflict`. The server answers the XML API, so decision 3 below
  names XML codes instead.

## Context

`Surl.Extended.Protocol.GoogleCloudStorage` (in `Protocols/FileTransfer/`, ADR-0083's table) answers
the `gs` scheme the Curl port adds, on port 443 (ADR-0083 decision 7). By ADR-0083 decision 1 it is
judged by the Cloud Storage XML API, not by upstream curl, which has no `gs` scheme, and not by the
Curl port. The pages read on 2026-11-05 (now served from `docs.cloud.google.com/storage/docs/…`,
redirected from `cloud.google.com`) are cited below by title:

- **"XML API overview"** (`xml-api/overview`): the request endpoints, path-style
  `storage.googleapis.com/<bucket>/<object>` and virtual-hosted `<bucket>.storage.googleapis.com/<object>`,
  over HTTP or HTTPS. It says the `Authorization` header is the usual login, gives a Bearer example,
  and allows anonymous access to public data.
- **"GET Bucket (List Objects)"** (`xml-api/get-bucket-list`): `list-type` (default 1),
  `continuation-token`, `delimiter`, `encoding-type`, `fetch-owner`, `marker`, `max-keys`, `prefix`
  and `start-after`. Results come in lexicographic name order. The body is `ListBucketResult` in the
  namespace `http://doc.s3.amazonaws.com/2006-03-01`, and each `Contents` holds `Key`, `Generation`,
  `MetaGeneration`, `LastModified`, `ETag` and `Size`.
- **"GET Object"** (`xml-api/get-object-download`), **"HEAD Object"**, **"PUT Object"** and
  **"DELETE Object"**: `Range`, `If-Match`, `If-None-Match`, `If-Modified-Since`,
  `If-Unmodified-Since`, `x-goog-if-generation-match`, `x-goog-if-metageneration-match` and the
  `generation` query parameter.
- **"HTTP status and error codes"** (`xml-api/reference-status`): the
  `<?xml version='1.0' encoding='utf-8'?><Error><Code>…</Code><Message>…</Message></Error>` body, and
  every code with its status. Two differ from S3: `NotImplemented` is **400**, and a missing
  credential is **401 `AuthenticationRequired`**.
- **"HTTP headers and common request headers"** (`xml-api/reference-headers`): `x-goog-hash`
  (`crc32c=<base64>,md5=<base64>`, CRC-32C big-endian), `x-goog-generation`,
  `x-goog-metageneration` (1 for a new object), `x-goog-stored-content-length`,
  `x-goog-stored-content-encoding` (`identity` when none is stored), `x-goog-storage-class`,
  `x-goog-meta-*` (8 KiB of custom metadata in total), `x-goog-date` (V4: `yyyyMMdd'T'HHmmss'Z'`),
  `x-goog-content-sha256` ("required for requests signed through the Authorization header"; the hex
  SHA-256 or `UNSIGNED-PAYLOAD`), `x-goog-project-id` and `Content-MD5`.
- **"V4 signing process"** (`authentication/signatures`) and its **"Canonical requests"** page:
  `GOOG4-HMAC-SHA256`, the scope `<yyyyMMdd>/<location>/storage/goog4_request`, the signing key
  chained from `GOOG4<secret>`, and a 15-minute allowed skew.

Which requests a real client sends, as background and not as a verdict: the port's
`Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` sends path-style GET Object, PUT Object (one
request, `Content-Length`), and List Objects with `list-type=2`, `delimiter=/` and `prefix`. It follows
`NextContinuationToken`, or `NextMarker` back as `marker`. It logs in with `Authorization: Bearer`, a
`GOOG4-HMAC-SHA256` header, or nothing. Its endpoint is `storage.googleapis.com` over HTTPS, or the
`http://` or `https://` URL that `STORAGE_EMULATOR_HOST` names.

## Decision

### 1. The operations served, and the one answer for any other

An operation is recognised by its method, by whether the path names a bucket and an object, and by
its query parameters, as each XML API page's request syntax writes it:

| Operation | Request | Success | Page | Task |
| --- | --- | --- | --- | --- |
| List Buckets (GET Service) | `GET /` on the service | `200`, `ListAllMyBucketsResult` | "GET Service" | BL-431 |
| List Objects | `GET /<bucket>`, with or without `list-type=2` | `200`, `ListBucketResult` (decision 6) | "GET Bucket (List Objects)" | BL-431 |
| GET Object | `GET /<bucket>/<object>` | `200`, or `206` for a range | "GET Object" | BL-431 |
| HEAD Object | `HEAD /<bucket>/<object>` | GET Object's head, no body | "HEAD Object" | BL-431 |
| PUT Object | `PUT /<bucket>/<object>` | `200`, `ETag`, `x-goog-generation`, `x-goog-hash`, no body | "PUT Object" | BL-431 |
| DELETE Object | `DELETE /<bucket>/<object>` | `204`; a missing object is `404 NoSuchKey` | "DELETE Object" | BL-431 |

- **Every other request** is answered **`400 Bad Request`, `<Code>NotImplemented</Code>`**, with the
  Message `A header or query you provided requested a function that is not implemented.` ("HTTP
  status and error codes": `NotImplemented`, 400). That covers another method, a bucket or object
  subresource (`?acl`, `?cors`, `?lifecycle`, `?logging`, `?versioning`, `?billing`, `?encryption`,
  `?storageClass`, `?location`, `?tagging`, `?uploads`, `?uploadId`, `?versions`, `?generation=` on
  a write, `?delete`), bucket creation and deletion, a `PUT` with `x-goog-copy-source` (copy), a
  `POST` (form upload, multipart and resumable initiation), `OPTIONS`, and the customer-supplied
  encryption headers `x-goog-encryption-*`. The check runs after authentication (decision 3), so a
  peer without a login learns nothing about which operations exist.
- **Buckets are not created or deleted over `gs`.** A bucket is a top-level folder (decision 2).
  `--directory`'s contents, another protocol (FTP `MKD`, SFTP `mkdir`, SMB) or `--bucket` (ADR-0102
  decision 2.4) makes one. The reason is ADR-0108 decision 1's, and it holds here too: every file
  server serves the same top-level folders.
- **`generation` on a read** names a version. The store keeps one version a file, so a `generation`
  equal to the current one is served, and any other is **`404 NoSuchKey`**: that version does not
  exist, as GET Object says of a generation that is gone.
- **Conditions** on GET Object and HEAD Object follow the GET Object page: a failing `If-Match` is
  `412 PreconditionFailed`, a matching `If-None-Match` is `304 Not Modified`, an unmodified
  `If-Modified-Since` is `304`, and a failing `If-Unmodified-Since` is `412`, with ADR-0108 decision
  1's precedence. A failing `x-goog-if-generation-match` or `x-goog-if-metageneration-match` is `412`
  on every object operation. On PUT Object, `x-goog-if-generation-match: 0` stores the object only
  when none exists ("HTTP headers": "0 … only if the object does not already exist"). A non-numeric
  value is **`400 InvalidArgument`**.
- **Range.** One `Range: bytes=` range is `206` with `Content-Range`, and an unsatisfiable one is
  **`416 InvalidRange`**. Several ranges, or a syntax the page does not give, are ignored and the
  whole object is served with `200`, as ADR-0108 decides. `x-goog-hash` on a `206` still describes
  the whole object (GET Object: "checksums cover the full object").
- **`x-goog-project-id`** is accepted and ignored on every request. Surl has one project, and the
  page makes the header optional "if a default project is set".

#### 1.1 The object's headers ("HTTP headers and common request headers")

GET Object and HEAD Object carry `Content-Length`, `Content-Type`, `ETag`, `Last-Modified` (an
IMF-fixdate, the file's last-write time), `Accept-Ranges: bytes`, each stored `Content-Encoding`,
`Content-Disposition`, `Cache-Control` and `Content-Language`, each stored `x-goog-meta-*`, and:

| Header | Value |
| --- | --- |
| `x-goog-generation` | the index's generation (ADR-0100 decision 2.3: the last-write time in microseconds since the Unix epoch for a file with no entry; the upload time in microseconds for a PUT, so it changes whenever the data is replaced) |
| `x-goog-metageneration` | `1` (no request changes metadata alone, so a generation never reaches 2) |
| `x-goog-stored-content-length` | the file's length |
| `x-goog-stored-content-encoding` | the stored `Content-Encoding`, else `identity` |
| `x-goog-storage-class` | `STANDARD` |
| `x-goog-hash` | `crc32c=<base64 of the big-endian CRC-32C>,md5=<base64 of the MD5>` |

- The ETag is ADR-0100 decision 2.3's quoted lower-case hexadecimal MD5. `x-goog-hash`'s `md5` is the
  base64 of the same digest.
- The CRC-32C is computed with `System.Numerics.BitOperations.Crc32C`, which is in the BCL, and is
  cached in the index entry beside the MD5. A file with no entry has both computed on its first read.
- No response carries `x-guploader-uploadid` or another Google-internal identifier. `Surl.HttpServing`
  writes `Server` and `Date` for every server (ADR-0006 section 3).

#### 1.2 PUT Object

- The body is stored through the content store's upload session. It replaces an existing file, and
  the index entry is rewritten. The server stores `Content-Type` (default `application/octet-stream`
  when none is sent), `Content-Encoding`, `Content-Disposition`, `Cache-Control`, `Content-Language`
  and each `x-goog-meta-*`, with names lower-cased.
- `Content-MD5` is checked when sent: a mismatch is **`400 BadDigest`**, and a value that is not the
  base64 of 16 bytes is **`400 InvalidDigest`**. Each `x-goog-hash` value (`md5`, `crc32c`) is checked
  when sent: a mismatch is **`400 BadDigest`**, and a malformed one is **`400 InvalidHash`**. A failed
  check stores nothing.
- `x-goog-storage-class` is accepted, and `STANDARD` is what every later read reports: the store has
  one class. `x-goog-acl` and the `x-goog-grant-*` headers are accepted and ignored, because ACLs are
  ADR-0102's share access, not object ACLs. An object key ending in `/` with an empty body creates the
  folder, and a non-empty one is refused (ADR-0100 decision 2.2).
- A body shorter than `Content-Length` when the peer closes is **`400 IncompleteBody`**, and nothing is
  stored.

#### 1.3 The error body ("HTTP status and error codes")

```
<?xml version='1.0' encoding='UTF-8'?><Error><Code>NoSuchKey</Code><Message>The specified key does not exist.</Message></Error>
```

- `Content-Type: application/xml; charset=UTF-8`, the body on one line, UTF-8, with the page's
  single-quoted XML declaration.
- `Code` and `Message` are fixed for each code, from the table below (ADR-0006 section 3: fixed text).
  **No client byte is echoed**, so no `Details`, `Resource` or `StringToSign` element is sent. The page
  documents only `Code` and `Message`. A `Details` element naming the object would echo the client's
  path, and a string to sign would tell a probing peer how the request was canonicalised.
- A `HEAD` error has its status and no body.

The codes the server sends, each with the status the page gives it:

| Code | Status | Message | When |
| --- | --- | --- | --- |
| `AccessDenied` | 403 | `Access denied.` | A logged-in write while `--allow-uploads` is off; a write that a share's access forbids (ADR-0102 decision 2.3) |
| `AuthenticationRequired` | 401 | `Authentication required.` | No credential with anonymous off; an anonymous write under `--allow-anonymous` |
| `BadDigest` | 400 | `The Content-MD5 or x-goog-hash you specified did not match what was received.` | Decision 1.2; a body not hashing to `x-goog-content-sha256` (decision 3.3) |
| `EntityTooLarge` | 400 | `Your proposed upload exceeds the maximum allowed object size.` | A body past `--max-filesize` (decision 7) |
| `IncompleteBody` | 400 | `You did not provide the number of bytes specified by the Content-Length HTTP header.` | Decision 1.2 |
| `InsufficientQuota` | 403 | `The object metadata index is full.` | ADR-0100's object metadata index is full |
| `InvalidAccessKeyId` | 403 | `The access key ID you provided does not exist in our records.` | A V4 HMAC access ID that is no account (decision 3.3) |
| `InvalidArgument` | 400 | `Invalid argument.` | A bad query or condition value (`max-keys`, `continuation-token`, `encoding-type`, `generation`, `x-goog-if-*`); a `GOOG4-HMAC-SHA256` header that does not parse, or whose scope does not end `/storage/goog4_request` |
| `InvalidAuthentication` | 400 or 401 | `The provided authentication header is invalid.` | 400: an `Authorization` scheme other than Bearer and `GOOG4-HMAC-SHA256`. 401: a Bearer token that is no account, or one sent over plain text without `--allow-plaintext-auth` (decision 5) |
| `InvalidDigest` | 400 | `The Content-MD5 you specified is not valid.` | Decision 1.2 |
| `InvalidHash` | 400 | `The x-goog-hash you specified is not valid.` | Decision 1.2 |
| `InvalidObjectName` | 400 or 409 | `The specified object name is not valid.` | 400: an object name the store cannot hold, on a write. 409: one conflicting with a folder or file of the other kind (decision 2) |
| `InvalidRange` | 416 | `The requested range cannot be satisfied.` | An unsatisfiable range |
| `KeyTooLong` | 400 | `Your key is too long.` | An object name over 1024 UTF-8 bytes |
| `MaxMessageLengthExceeded` | 400 | `Your request was too big.` | A request head past `--max-request-head` (decision 7) |
| `MetadataTooLarge` | 400 | `Your metadata headers exceed the maximum allowed metadata size.` | `x-goog-meta-*` names and values past 8 KiB |
| `MissingContentLength` | 411 | `You must provide the Content-Length HTTP header.` | A `PUT` without `Content-Length` (a chunked body included) |
| `MissingSecurityHeader` | 400 | `Your request was missing a required header.` | A `GOOG4-HMAC-SHA256` request without `x-goog-date` (or `Date`), or without `x-goog-content-sha256` |
| `NoSuchBucket` | 404 | `The specified bucket does not exist.` | No such top-level folder, or the caller may not read its share |
| `NoSuchKey` | 404 | `The specified key does not exist.` | No file at the name, the name is a folder, the name cannot be held, or a `generation` that is not the current one |
| `NotImplemented` | 400 | `A header or query you provided requested a function that is not implemented.` | Decision 1's every other request |
| `PreconditionFailed` | 412 | `At least one of the pre-conditions you specified did not hold.` | A failing `If-Match`, `If-Unmodified-Since` or `x-goog-if-*` |
| `RequestTimeTooSkewed` | 403 | `The difference between the request time and the server's time is too large.` | `x-goog-date` more than 15 minutes from the server's clock |
| `SignatureDoesNotMatch` | 403 | `The request signature we calculated does not match the signature you provided.` | A V4 HMAC signature that does not verify |
| `SlowDown` | 503 | `Please reduce your request rate.` | A connection past `--max-connections` or `--max-connections-per-address` (decision 7) |

The page gives each code's meaning but no fixed message, so the Messages above are Surl's own. Each
one restates the page's description in one sentence.

### 2. Path-style and virtual-hosted addressing, and the bucket mapping ("XML API overview")

- **Path style** is always served: the path's first segment is the bucket and the rest is the object
  name (`/photos/2026/a.jpg`). `/` alone is the service (List Buckets).
- **Virtual-hosted style** is served when the `Host` field's name, with the port removed, case folded
  and no trailing dot, is `<bucket>.<domain>` for one of the server's virtual-hosting domains. The
  whole path is then the object name, and `/` is the bucket (List Objects). `<bucket>` is everything
  before the domain, dots included.
- **The virtual-hosting domains** follow ADR-0108 decision 2's rule, in this order:
  1. the listen URL's host, when it is a DNS name and not an IP literal or a wildcard (`surl
     gs://gcs.test:4443` serves `photos.gcs.test:4443`), passed to the server's constructor by the
     composition;
  2. `localhost`;
  3. Google's endpoint, `storage.googleapis.com`. A client pointed at Surl by `--resolve`,
     `--connect-to`, a hosts file or `STORAGE_EMULATOR_HOST` then sends its unmodified requests. A
     `Host` that names `storage.googleapis.com` itself is path style.

  No option is added.
- **Any other `Host`**, whether an IP literal, a name matching no domain, or no `Host` at all, is path
  style. This is what an emulator client sends: `STORAGE_EMULATOR_HOST=http://127.0.0.1:4443` gives
  `Host: 127.0.0.1:4443` and a path-style request.
- **Bucket = top-level folder** of the one shared `ContentStore` (ADR-0100 decision 2), resolved
  through `IContentShareLookup` (ADR-0100 decision 2.6), so `--bucket <name>=<path>` (ADR-0102
  decision 2.4) needs no `gs` change. A top-level file is not a bucket, and `.surl` is never one.
- **Object `x/y/z` = file `z` in folder `x/y`** under the bucket's folder. The name is
  percent-decoded once from the path as UTF-8 ("Canonical requests": the path is UTF-8,
  percent-encoded once).
- **Names the store cannot hold** follow ADR-0100 decision 2.2's list. A write of one is **`400
  InvalidObjectName`**, and a name whose first segment would shadow an entry of the other kind is
  **`409 InvalidObjectName`**. A read of either is `404 NoSuchKey`. A name over 1024 UTF-8 bytes (the
  Cloud Storage object naming limit) is **`400 KeyTooLong`** on every operation.
- **Share access** (ADR-0102 decision 2.3): a bucket or object the caller may not read is `404
  NoSuchBucket` or `NoSuchKey`, and a write the caller may not make is `403 AccessDenied`. Every
  store call names the caller (the bearer account, the HMAC account, or anonymous) and the scheme
  `gs`.
- **A file written by another protocol** is served with ADR-0100 decision 2.3's defaults:
  `Content-Type` from the extension, no user metadata, the MD5 ETag, and the last-write-time
  generation. An object PUT over `gs` is a file every other file server serves.

### 3. Authentication

#### 3.1 What is required

- **By default every request carries a valid login** of the one account set (`--user`,
  `--user-file`; ADR-0100 decision 4.1). That is either `Authorization: Bearer <token>` (decision 3.2)
  or `Authorization: GOOG4-HMAC-SHA256 …` (decision 3.3).
- **No credential, with anonymous off,** is **`401 AuthenticationRequired`** ("HTTP status and error
  codes": "the request needs a credential, and none was provided"). It carries `WWW-Authenticate:
  Bearer realm="storage"`, because RFC 9110 section 15.5.2 requires a `401` to carry a challenge and
  RFC 6750 section 3 names Bearer's. The realm is a fixed word naming the service.
- **`--allow-anonymous`** serves unauthenticated List Buckets, List Objects, GET Object and HEAD
  Object, as the overview's anonymous access to public data does. An anonymous PUT or DELETE is still
  **`401 AuthenticationRequired`** with the challenge. A request that carries credentials is checked
  even then, and a bad one is never served as anonymous.
- **Writes** need `--allow-uploads` (ADR-0006 section 2) **and** a login. With uploads off, a
  logged-in write is **`403 AccessDenied`** (ADR-0102 decision 2.3's `NotPermitted`).
- **Order:** the request head and limits, then addressing, then authentication, then the operation,
  the bucket, the share access and the name. A peer without a login learns only decision 3.4's
  answers.
- **Each refusal waits the failed-login delay** that the authentication session already applies
  (ADR-0032), then answers. The connection stays open.

#### 3.2 Bearer tokens (RFC 6750)

Bearer tokens are checked by `Surl.Authentication`'s existing `BearerAuthenticationMethod` against
the empty-name accounts' passwords (ADR-0100 decision 4.1's GoogleCloudStorage row), in constant time
(ADR-0032 section 1), exactly as ADR-0109 decision 3.1 does for Azure. A token is opaque: no JSON Web
Token or Google token-info lookup is involved, because Surl issues no Google tokens and a harness hands
its client whatever string it configured (`--user ':<token>'`). A token that is no account is
**`401 InvalidAuthentication`** with `WWW-Authenticate: Bearer realm="storage", error="invalid_token"`
(RFC 6750 section 3.1).

#### 3.3 V4 HMAC signatures ("V4 signing process", "Canonical requests")

- **The `Authorization` header form only**: `GOOG4-HMAC-SHA256 Credential=<access id>/<yyyyMMdd>/<location>/storage/goog4_request,
  SignedHeaders=<names>, Signature=<hex>`. The HMAC key comes from the account set (name = access ID,
  password = secret; ADR-0100 decision 4.1).
- **BL-430** adds the check to `Surl.Authentication` beside `AwsSigV4AuthenticationMethod`. It shares
  the canonical-request code, parameterised by the algorithm name (`GOOG4-HMAC-SHA256`), the date
  header (`x-goog-date`), the content-hash header (`x-goog-content-sha256`), the key prefix (`GOOG4`)
  and the scope terminator (`storage/goog4_request`). The AWS bytes do not change. It is reached
  through `IHttpAuthenticationSession`, and the server references neither `Surl.Authentication` nor
  an account (ADR-0084 decision 4).
- **Rules:** the canonical URI is encoded once and the query is re-encoded and sorted. `host` must be
  a signed header. The payload line is `x-goog-content-sha256`, the lower-case hex SHA-256 or
  `UNSIGNED-PAYLOAD`, and the header is **required** ("HTTP headers": "required for requests signed
  through the Authorization header"). The date is `x-goog-date` in `yyyyMMdd'T'HHmmss'Z'`, or
  `Date` when `x-goog-date` is absent, and must equal the scope's date and lie within 15 minutes
  either way on the injected `TimeProvider`. Any location in the scope (`auto`, `us-east1`, …) is
  accepted: the server is every location at once.
- **A body with a hex hash is bound once it is read** (ADR-0045). A mismatch is **`400 BadDigest`**,
  and nothing is stored.
- **`InvalidAccessKeyId` is sent for an unknown access ID**, as the page lists it, for ADR-0108
  decision 4.3's reason. The check still runs against a random secret, so the two answers take the
  same time.
- **The refusal reason** comes from the `HttpAuthenticationVerdict` refusal that ADR-0108 decision 4.4
  adds (BL-420). BL-430 sets it from the GOOG4 method, and BL-432 maps it through decision 3.4. No
  new contract is added.

#### 3.4 Each refusal

| Failure | Status, code |
| --- | --- |
| No credential, anonymous off; or an anonymous write | `401 AuthenticationRequired` + `WWW-Authenticate: Bearer realm="storage"` |
| `Authorization` with another scheme (Basic, Digest, `AWS4-HMAC-SHA256`, the V2 `GOOG1` and `AWS` forms) | `400 InvalidAuthentication` |
| Bearer token that is no account | `401 InvalidAuthentication` + `error="invalid_token"` challenge |
| Bearer token over plain text without `--allow-plaintext-auth` | `401 InvalidAuthentication` + `error="invalid_token"` challenge |
| `GOOG4-HMAC-SHA256` that does not parse, a scope not ending `/storage/goog4_request`, or `host` not signed | `400 InvalidArgument` |
| `GOOG4-HMAC-SHA256` without `x-goog-content-sha256`, or without `x-goog-date` and `Date` | `400 MissingSecurityHeader` |
| Access ID that is no account | `403 InvalidAccessKeyId` |
| Signature that does not verify | `403 SignatureDoesNotMatch` |
| Date more than 15 minutes off, or not the scope's date | `403 RequestTimeTooSkewed` |
| Body not hashing to `x-goog-content-sha256` | `400 BadDigest` |
| Logged-in write with `--allow-uploads` off, or forbidden by share access | `403 AccessDenied` |

- **How this differs from the HTTP server.** The HTTP server answers a missing or failed login with
  `401` and its own challenges, Digest first (ADR-0032 section 4). The `gs` server sends only the
  Bearer challenge above, and `--auth` does not apply to `gs`: Bearer and V4 HMAC are its methods.
- **How this differs from the S3 server.** S3 refuses a missing login with `403 AccessDenied`
  (ADR-0108). Cloud Storage's page gives `401 AuthenticationRequired`, and the server follows its own
  page.

### 4. Headers the request must carry

`Content-Length` on a `PUT` (decision 1.3's `MissingContentLength`), and on a V4 HMAC request the
headers of decision 3.3. `Date` is not required on a Bearer or anonymous request, although the GET
Bucket page's syntax shows it. Clients send it inconsistently, and only a signature binds it.
`x-goog-api-version` is ignored, because the headers page marks it deprecated.

### 5. TLS

- **`gs` is HTTPS**: implicit TLS from the first byte, as `storage.googleapis.com` is, wrapped in
  `ImplicitTlsSchemeServer` as `https` is (ADR-0010), on port 443 (ADR-0083 decision 7). It uses TLS
  1.2 and 1.3 (ADR-0006 section 4). `--cert` or `--self-signed` is required, and without one the start
  fails with `CertificateProblem` (58), as ADR-0032 decides for every secure listen URL. ALPN offers
  `http/1.1` only.
- **The option that changes it: `--gs-plaintext`** (no argument, category `gs`, off by default, and
  warned on start as ADR-0032's loosening options are). It serves `gs` as plain HTTP/1.1 for the
  emulator clients that `STORAGE_EMULATOR_HOST=http://…` points at Surl, as `--s3-plaintext` does
  for `s3` (ADR-0108 decision 5) and `--az-plaintext` for `az` (ADR-0109 decision 6).
- **A bearer token over plain text** is refused unless `--allow-plaintext-auth` is given (decision
  3.4), because a token is a secret in the clear (ADR-0032 section 3). A V4 HMAC signature sends no
  secret, so it is served over plain text.
- BL-433 adds `--gs-plaintext` with its `OptionArgumentType`, help category and `--aihelp` facts.

### 6. List Objects and List Buckets ("GET Bucket (List Objects)", "GET Service")

- **Both listing versions are served.** `list-type=2` is the version the port's client sends, and
  version 1 (with `marker` and `NextMarker`) is the page's default, and the port's client follows a
  `NextMarker` when one is sent. The two share one walk, and only the paging elements differ.
- **Namespace** `http://doc.s3.amazonaws.com/2006-03-01` on `ListBucketResult` and on
  `ListAllMyBucketsResult`, as the page's example writes it (not S3's `s3.amazonaws.com/doc/…`).
- **Elements**, in this order: `Name`, `Prefix`, then `Marker` (version 1, when given) or
  `ContinuationToken` and `StartAfter` (version 2, when given), then `KeyCount` (version 2), `MaxKeys`,
  `Delimiter` (when given), `IsTruncated`, then `NextMarker` (version 1, when truncated) or
  `NextContinuationToken` (version 2, when truncated), each `Contents` (`Key`, `Generation`,
  `MetaGeneration`, `LastModified` as `yyyy-MM-ddTHH:mm:ss.fffZ`, `ETag`, `Size`, and `Owner` with `ID`
  set to the account name or `anonymous` when version 2's `fetch-owner=true`), then each
  `CommonPrefixes` `Prefix`. `MaxKeys` is always written, so a client never has to guess the
  default.
- **Order:** names in ascending UTF-8 byte order (the page's lexicographic order), walked as ADR-0108
  decision 6 walks them.
- **`max-keys`** defaults to 1000, and a value above 1000 is served as 1000. `0` gives an empty,
  untruncated page. A negative or non-numeric value is `400 InvalidArgument`. The bound covers
  `Contents` and `CommonPrefixes` together.
- **`prefix`**, **`delimiter`** (any string) and **`encoding-type=url`** behave as in ADR-0108
  decision 6, and another `encoding-type` is `400 InvalidArgument`.
- **Version 1 paging:** `marker` begins after that name. A truncated page carries `NextMarker`, the
  last name or common prefix returned, which the page's clients send back as `marker`.
- **Version 2 paging:** `start-after` begins after that name. `continuation-token` is the base64url
  (no padding) of the UTF-8 bytes of the last entry returned, and wins over `start-after` (the page:
  `start-after` is ignored when `continuation-token` is present). A token that does not decode is `400
  InvalidArgument`. `marker` with `list-type=2`, and `continuation-token`, `start-after` or
  `fetch-owner` without it, are `400 InvalidArgument` (the page: "not valid with" or "valid only
  with").
- **`versions`, `key-marker`, `version-id-marker` and `generation-marker`** are `400 NotImplemented`:
  the store keeps one version a file.
- **Empty folders** are not objects, unless a client put `x/`, which the index records and lists as
  `x/` with size 0.
- **List Buckets** answers `ListAllMyBucketsResult` with `Owner` (`ID` the account name or
  `anonymous`) and one `Bucket` (`Name`, `CreationDate` as the folder's creation time) for each
  top-level folder the caller may read, in UTF-8 byte order.

### 7. Limits (ADR-0006)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| `--max-request-head` (100 KiB) | `Surl.HttpServing`'s head reader | `400 MaxMessageLengthExceeded`, then close |
| `--max-filesize` (100 MiB) | a PUT Object body, checked against `Content-Length` before the body is read and again while it is written | `400 EntityTooLarge`, then close (the body is not drained) |
| the largest object | `--max-filesize`, an "uploaded file" in ADR-0006's words. Cloud Storage's own 5 TiB cap is above any default and needs no second bound | as above |
| an object name | 1024 UTF-8 bytes | `400 KeyTooLong` |
| custom metadata | 8 KiB of `x-goog-meta-*` names and values ("HTTP headers"), equal to ADR-0100's per-object bound; ADR-0100's 100000 index entries | `400 MetadataTooLarge`; `403 InsufficientQuota` |
| a listing page | 1000 entries (decision 6) | served as 1000 |
| `--head-timeout` (30 s), `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.HttpServing`'s and `Surl.Core`'s, as for HTTP | close with no bytes |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s, through the server's refusal writer | `503 SlowDown` with the XML body, then close (ADR-0006 section 5: the protocol's own error) |

No other new option is added. Because there is no multipart or resumable upload (decision 1), no
staging bound is needed.

### 8. `--aihelp`

The topic is `gs` (ADR-0083 decision 8). Its about text says the server answers Google Cloud Storage
XML API requests on the shared content store. It says the server is judged by the Cloud Storage XML
API and not by upstream curl, that it requires a Bearer token (`--user ':<token>'`) or a V4 HMAC
signature by default, and that it is HTTPS unless `--gs-plaintext`. BL-433 writes it with its example.

## Alternatives considered

- **The JSON API (`/storage/v1/…`, `/upload/storage/v1/…`) beside the XML API.** Not taken here. The
  task, the port's client and the scheme's matrix are the XML API, and the JSON API is a second
  grammar and error shape. BL-719 decides it in its own ADR, on the same store and listener.
  ADR-0100's JSON planning row is replaced here only for the XML server.
- **Resumable and XML multipart uploads in BL-431.** Moved to their own task, BL-718. The port's client
  uploads in one PUT, and one PUT serves every upload up to `--max-filesize`. Until BL-718 lands they
  answer `400 NotImplemented`, and BL-718 amends decisions 1 and 7, as BL-713 adds S3's multipart
  uploads under ADR-0108.
- **Accepting `AWS4-HMAC-SHA256` on `gs`** (Cloud Storage's S3-interoperability form). Rejected for
  `gs`: the V4 signing page read for this ADR names `GOOG4-HMAC-SHA256` for HMAC keys, and an S3
  client already has `s3://` (ADR-0108).
- **`403 AccessDenied` for a missing credential, as S3 answers.** Rejected: the Cloud Storage page
  gives `401 AuthenticationRequired`.
- **Sending `Details` naming the object,** as Google's service does. Rejected: it echoes client bytes
  that the page does not require (ADR-0006 section 3).
- **A plain-HTTP `gs` by default, for emulator clients.** Rejected: ADR-0032 makes a secure listen URL
  the default, and `--gs-plaintext` keeps the emulator case one flag away.

## Consequences

Good:

- The `gs` server answers every request the port's client sends, version 1 listings and the
  object-integrity headers besides. It serves the same files, accounts and shares as FTP, SFTP, HTTP,
  SMB, S3 and Azure.
- Every refusal is one fixed row with its XML API code, so tests pin each byte from the pages.
- BL-430 reuses the SigV4 canonical-request code, so a fix to one signature scheme fixes both.

Costs and caveats:

- BL-430 changes `Surl.Authentication` and depends on BL-420's refusal reason.
- Resumable and XML multipart uploads answer `NotImplemented` until BL-718 lands. The JSON API waits on
  BL-719's decision.
- The matrix is `Documentation/Conformance/Extended/GoogleCloudStorage.md` (ADR-0083 decision 9).

## Amendment 1 (BL-430, 2026-11-05)

Decided by Claude under Stewart's delegation. What BL-430 built fills in decision 3.3's details:

- **Routing.** A request whose `AwsSigV4Service` is `storage` is checked by
  `GoogleV4HmacAuthenticationMethod` only when its `Authorization` scheme is `GOOG4-HMAC-SHA256`; any
  other Signature Version 4 scheme on `storage` is `UnsupportedScheme` (`400 InvalidAuthentication`).
  Every other service keeps the AWS check.
- **The date.** `x-goog-date`, else `Date`, is read in `yyyyMMdd'T'HHmmss'Z'` only, the form the string
  to sign carries; an RFC 1123 `Date` does not read and is `MalformedCredentials`
  (`400 InvalidArgument`). It need not be in `SignedHeaders`: the string to sign holds the request
  time, so the signature binds it anyway; only `host` must be signed. A date that reads but is not the
  scope's day is `ClockSkew` (`403 RequestTimeTooSkewed`), as decision 3.4 says, even within 15 minutes.
- **The missing date.** Neither field is `HttpAuthenticationRefusal.MissingRequestDate`, the refusal
  ADR-0109 decision 3.2 added for Azure Shared Key (BL-717); no new contract was needed.
- **Head checks in order:** parse, scope and `host` (malformed); missing date; missing content hash;
  date unreadable, then skewed; then the signature (an unknown access ID against a random secret).
