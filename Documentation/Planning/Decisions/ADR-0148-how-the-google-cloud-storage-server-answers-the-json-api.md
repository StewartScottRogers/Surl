# ADR-0148 — How the Google Cloud Storage server answers the JSON API

- **Status:** Accepted
- **Date:** 2026-11-11
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-11, in BL-719.
  The build is filed as BL-1045 to BL-1044 (decision 9). Amends ADR-0110 decisions 1, 1.1, 3 and 7 (the JSON paths are no
  longer unlisted, `x-goog-metageneration` can pass 1, the Bearer rules carry over, the staging
  bound is shared) and ADR-0100 decision 2.3 (the object metadata index records a metageneration).
- **Builds on:** [ADR-0110](ADR-0110-how-the-google-cloud-storage-server-answers-gs.md) (the XML API, its
  store, addressing, accounts and limits, which this ADR reuses), [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification, the Curl port is never proof),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  (the object metadata index, shared accounts), [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  (share access), [ADR-0124](ADR-0124-how-surl-api-observes-and-controls-the-servers-it-starts.md) and
  [ADR-0144](ADR-0144-how-the-coap-mcp-and-signalr-servers-answer-a-chosen-error.md) (chosen errors),
  [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits, fixed error text) and
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) (secure by default).

## Context

ADR-0110 serves the Cloud Storage XML API and left the JSON API to this task. It matters for Surl as a stand-in:
Google's own client libraries (Go, Python, Java, Node.js, .NET) and `gcloud storage` speak the JSON API, and
point at an emulator through `STORAGE_EMULATOR_HOST`. A test suite that wants a stand-in for Cloud Storage
reaches it that way, so the JSON API is the form most of them need. The Curl port's client speaks the XML API
only (background, never a verdict: ADR-0083).

The specification is the Cloud Storage JSON API reference, read for this ADR: "Objects: get, insert, list,
delete, patch, update, copy, rewrite, compose", "Buckets: list, get", "HTTP status and error codes" (the JSON
tab, with its `error` body and `reason` values), and the upload pages ("Perform a media upload", "Perform a
multipart upload", "Perform resumable uploads" with its JSON API tab). Upstream curl is not an oracle for
any of it: it makes no Cloud Storage requests of its own, so each row is judged by the pages.

## Decision

### 1. One listener, two grammars, told apart by the path

The JSON API is served on the same listener, store, accounts and limits as the XML API. A request is JSON when
its path, in path-style addressing, begins with `/storage/v1/`, `/upload/storage/v1/` or
`/download/storage/v1/`. Anything else is the XML API, unchanged.

- **JSON paths win.** A bucket named `storage`, `upload` or `download` with an object name beginning `v1/` is
  reached over the XML API by virtual-hosted style (ADR-0110 decision 2) only. It is a cost accepted once,
  as Google's own endpoint accepts it: the three words are reserved first segments there too.
- **Virtual-hosted style serves the XML API only**, as Google's JSON API is served from the path-style
  endpoint alone. A `Host` of `<bucket>.<domain>` with a path beginning `/storage/v1/` names an object, not
  the JSON API.
- **The decision is made before authentication**, because the two grammars refuse differently (decision 4),
  and **after** the request head and its limits.
- **No option and no scheme is added.** `gs` serves both grammars; `--gs-plaintext` (ADR-0110 decision 5)
  covers `STORAGE_EMULATOR_HOST=http://…`. `SURL_EXTENSIONS=0` turns off both, as it does the server.

### 2. The operations served

Paths are written without the leading `/storage/v1`. `<object>` is everything after `/o/`, percent-decoded
once as UTF-8, so a literal `/` and `%2F` both name a folder separator (client libraries send either).

| Operation (method id) | Request | Success |
| --- | --- | --- |
| `storage.buckets.list` | `GET /storage/v1/b?project=<id>` | `200`, `storage#buckets` |
| `storage.buckets.get` | `GET /storage/v1/b/<bucket>` | `200`, `storage#bucket` |
| `storage.objects.list` | `GET /storage/v1/b/<bucket>/o` | `200`, `storage#objects` |
| `storage.objects.get` | `GET /storage/v1/b/<bucket>/o/<object>` | `200`, `storage#object` |
| `storage.objects.get` with `alt=media` | the same, or `GET /download/storage/v1/b/<bucket>/o/<object>?alt=media` | `200` or `206`, the object's bytes |
| `storage.objects.insert`, `uploadType=media` | `POST /upload/storage/v1/b/<bucket>/o?uploadType=media&name=<object>` | `200`, `storage#object` |
| `storage.objects.insert`, `uploadType=multipart` | `POST /upload/storage/v1/b/<bucket>/o?uploadType=multipart`, `multipart/related` body | `200`, `storage#object` |
| `storage.objects.insert`, `uploadType=resumable` | `POST …?uploadType=resumable`, then `PUT` of chunks to the session URI | `200` with `Location`; `308`; `200`, `storage#object` |
| `storage.objects.delete` | `DELETE /storage/v1/b/<bucket>/o/<object>` | `204` |
| `storage.objects.patch` | `PATCH /storage/v1/b/<bucket>/o/<object>` | `200`, `storage#object` |
| `storage.objects.update` | `PUT /storage/v1/b/<bucket>/o/<object>` | `200`, `storage#object` |
| `storage.objects.copy` | `POST …/o/<object>/copyTo/b/<destBucket>/o/<destObject>` | `200`, `storage#object` |
| `storage.objects.rewrite` | `POST …/o/<object>/rewriteTo/b/<destBucket>/o/<destObject>` | `200`, `storage#rewriteResponse` |
| `storage.objects.compose` | `POST /storage/v1/b/<bucket>/o/<destObject>/compose` | `200`, `storage#object` |

- **Every other request** on a JSON path is **`501 Not Implemented`**, reason `notImplemented`, message
  `Not implemented.` That covers bucket creation, update, patch and deletion, ACLs and default ACLs, IAM,
  notifications, HMAC keys, `watchAll`, `restore`, `bulkRestore`, `moveTo`, anywhere an unlisted method
  meets a listed path, and every query parameter this ADR does not give (`versions=true`, `matchGlob`,
  `softDeleted`, `kmsKeyName`, `userProject` and the rest). The check runs after authentication (decision 4).
- **Buckets are not created or deleted**, for ADR-0110 decision 1's reason: a bucket is a top-level
  folder every file server shares. A test seeds one with `--bucket`, `--directory`, or another protocol.
  `storage.buckets.list` needs `project`, as the page says (`400 required` without it), accepts any value
  and ignores it, as `x-goog-project-id` is ignored on the XML API.
- **`fields`, `prettyPrint`, `alt=json`, `projection`, `quotaUser`, `userIp` and `key`** are accepted and
  ignored. A full resource is a valid answer to a partial-response request, and no client treats extra
  fields as an error. `alt=media` is the one `alt` that changes the answer, and any other value but `json`
  is `400 invalidParameter`.
- **`access_token` and `oauth_token` query parameters** are not accepted (decision 4).

### 3. Resources

Resources are written with `Utf8JsonWriter` and read with `JsonDocument` and `Utf8JsonReader`: BCL, no
reflection, AOT-safe (root `CLAUDE.md`). Member order is the order below. Times are RFC 3339 with
milliseconds (`2026-11-11T09:30:00.000Z`). 64-bit numbers are JSON strings, as the reference writes them.

- **`storage#object`**: `kind`, `id` (`<bucket>/<name>/<generation>`), `selfLink`, `mediaLink`, `name`,
  `bucket`, `generation`, `metageneration`, `contentType`, `storageClass` (`STANDARD`), `size`, `md5Hash`
  (base64 of the MD5), `crc32c` (base64 of the big-endian CRC-32C), `etag` (the base64 of the UTF-8
  `<generation>/<metageneration>`: opaque, and changing exactly when either does), `timeCreated`,
  `updated`, `timeStorageClassUpdated`, then `contentEncoding`, `contentDisposition`, `cacheControl`,
  `contentLanguage` and `metadata` (an object of the stored `x-goog-meta-*` pairs) when stored.
  `selfLink` and `mediaLink` use the request's scheme and `Host` (ADR-0110 amendment 2's session-URI rule).
  The values are ADR-0110 decision 1.1's, so an object PUT over the XML API reads back the same
  generation, MD5, CRC-32C and metadata over JSON, and the reverse.
- **`storage#bucket`**: `kind`, `id`, `name`, `selfLink`, `projectNumber` (`"0"`), `metageneration` (`"1"`),
  `location` (`US`), `storageClass` (`STANDARD`), `etag` (`CAE=`), `timeCreated`, `updated`. The times
  are the folder's creation and last-write times.
- **`storage#objects`**: `kind`, `nextPageToken` (when truncated), `prefixes` (when a `delimiter` finds
  any), `items`. **`storage#buckets`**: `kind`, `nextPageToken`, `items`.
- **Listing** follows ADR-0110 decision 6's walk, in UTF-8 byte order: `prefix`, `delimiter`,
  `includeTrailingDelimiter`, `startOffset` (inclusive), `endOffset` (exclusive), `maxResults` (default and
  cap 1000, `0` an empty page; a negative or non-numeric value is `400 invalidParameter`), and
  `pageToken` (base64url, no padding, of the last entry returned, as decision 6's `continuation-token`; one
  that does not decode is `400 invalidParameter`). `maxResults` bounds `items` and `prefixes` together. A
  folder is no object unless a client inserted `x/`, as for the XML API. Buckets list the top-level folders the
  caller may read.
- **The object's metadata**, on insert, patch and update, is `contentType`, `contentEncoding`,
  `contentDisposition`, `cacheControl`, `contentLanguage` and `metadata` (string values, 8 KiB of names and
  values together, `400 invalid` past it). Other writable fields (`acl`, `customTime`, `retention`, `kmsKeyName`,
  `eventBasedHold`, …) are ignored on a write and absent from a read; `storageClass` other than `STANDARD`
  is accepted and read back as `STANDARD`, as ADR-0110 decision 1.2 does. `md5Hash` and `crc32c` sent
  with an upload are checked against the bytes, and a mismatch is **`400 invalid`**, as `BadDigest`.
- **Metageneration** is new. The object metadata index records it (ADR-0100 decision 2.3, which only
  grows): `1` when the data is written, **+1 on every patch or update**, and an XML-API read reports the
  recorded value in `x-goog-metageneration`. A file with no entry reports `1`. Replacing the data resets
  it to `1` with the new generation.

### 4. Authentication

- **The same accounts and the same Bearer rules as ADR-0110 decisions 3.1, 3.2 and 3.4**: a valid
  `Authorization: Bearer <token>` of the empty-name accounts, checked in constant time by
  `Surl.Authentication`, over TLS unless `--allow-plaintext-auth`; `--allow-anonymous` serves
  anonymous reads (`buckets.list`, `buckets.get`, `objects.list`, `objects.get` and `alt=media`); a write
  needs `--allow-uploads` and a login; share access (ADR-0102 decision 2.3) judges each bucket and object
  and each store call names the caller and the scheme `gs`.
- **The JSON API accepts Bearer tokens only.** A `GOOG4-HMAC-SHA256` header, which the JSON endpoint does
  not take from Google either, is refused like an unknown scheme: `400 invalid`, as below. A token
  in the URL (`access_token`) is never taken, because a URL lands in logs and the Bearer header is the
  RFC 6750 form ADR-0032 requires.
- **Refusals**, each fixed and each with the `WWW-Authenticate: Bearer realm="storage"` challenge when it is a `401`:

| Failure | Answer |
| --- | --- |
| No credential, anonymous off; or an anonymous write | `401`, reason `required`, message `Authentication required.` |
| Bearer token that is no account, or over plain text without `--allow-plaintext-auth` | `401`, reason `authError`, message `Invalid Credentials`, challenge with `error="invalid_token"` |
| `Authorization` with another scheme (Basic, Digest, `GOOG4-HMAC-SHA256`, `AWS4-HMAC-SHA256`) | `400`, reason `invalid`, message `The provided authentication header is invalid.` |
| Logged-in write with `--allow-uploads` off, or forbidden by share access | `403`, reason `forbidden`, message `Access denied.` |
| A bucket or object the caller may not read | `404`, reason `notFound`, as if it were absent (ADR-0102) |

  Each refusal waits the failed-login delay (ADR-0032) first. The Google text `Anonymous caller does not
  have … access` names the caller's missing permission; Surl sends its own fixed sentence and none of the
  client's bytes (ADR-0006 section 3).

### 5. The error body ("HTTP status and error codes", JSON)

```
{"error":{"code":404,"message":"Not Found","errors":[{"message":"Not Found","domain":"global","reason":"notFound"}]}}
```

- `Content-Type: application/json; charset=UTF-8`, one line, UTF-8. `code` is the HTTP status; `errors` holds one
  entry with `domain` `global` (`usageLimits` for `503`/`429` rate answers), the fixed `message` and the `reason`.
  A `HEAD`-style answer without a body does not arise: the JSON API has no `HEAD`. A `HEAD` to a JSON path is
  `501 notImplemented` with its body, because the reference lists no such method and a client library never
  sends one.
- **No client byte is echoed**: no bucket, object, parameter name or token in `message`, `location` or
  `locationType`. Google names the offending parameter; Surl does not, so a test that wants to know which
  parameter was wrong reads the server's recorded exchange (ADR-0124), where the request is whole.
- **The rows** (status, reason, fixed message), each used where the page gives the reason:

| Status | Reason | Message | When |
| --- | --- | --- | --- |
| 304 | `notModified` | `Not Modified` | A matching `If-None-Match` or unmodified `If-Modified-Since` on `alt=media`; no body |
| 400 | `invalid` | `Invalid request.` | A body that is not the JSON the method takes; an over-long name; an upload hash mismatch; an unsupported authentication scheme; metadata past 8 KiB |
| 400 | `invalidParameter` | `Invalid parameter.` | A malformed query value (`maxResults`, `pageToken`, `generation`, `if*Match`, `alt`, `uploadType`) |
| 400 | `required` | `Required parameter missing.` | `project` on `buckets.list`; `name` on a media upload; no `name` in a multipart or resumable upload's metadata; a compose without sources |
| 400 | `badRequest` | `Bad request.` | A `Content-Range` that does not parse or a chunk of the wrong length; a malformed `multipart/related` body; a missing `Content-Length` on a media upload's `POST` is `411` below |
| 401 | `required`, `authError` | decision 4 | |
| 403 | `forbidden` | `Access denied.` | Decision 4; a full object metadata index is `403 quotaExceeded`, `The object metadata index is full.` |
| 404 | `notFound` | `Not Found` | No such bucket, object, `generation` other than the current one, or upload session; a name the store cannot hold on a read |
| 409 | `conflict` | `Conflict.` | A name conflicting with a folder or file of the other kind (ADR-0110 decision 2); a compose source or copy destination in conflict |
| 411 | `lengthRequired` | `Length required.` | A media or multipart `POST`, or a chunk, without `Content-Length` (a chunked body included) |
| 412 | `conditionNotMet` | `Precondition Failed` | A failing `ifGenerationMatch`, `ifGenerationNotMatch`, `ifMetagenerationMatch`, `ifMetagenerationNotMatch`, `If-Match` or `If-Unmodified-Since` |
| 413 | `uploadTooLarge` | `Payload too large.` | A body past `--max-filesize` (decision 7) |
| 416 | `requestedRangeNotSatisfiable` | `Requested range not satisfiable.` | An unsatisfiable `Range` on `alt=media` |
| 501 | `notImplemented` | `Not implemented.` | Decision 2's every other request |
| 503 | `backendError` | `Please reduce your request rate.` | A connection past `--max-connections` or `--max-connections-per-address`, and open uploads past 1000 |

- **Conditions** are the query parameters the pages name, with ADR-0110 decision 1's precedence and the
  same current generation and metageneration; `ifGenerationMatch=0` on an insert, copy, rewrite or compose
  stores only when no object exists.
- **A chosen fault** (ADR-0124, ADR-0144) is asked of the plan per request with the **command** the Google method id
  (`storage.objects.insert`, …) and the **path** the request path. A reply status from `400` to `599` is
  sent with the row above whose status matches, or `backendError` with the code when none does; a reply that
  does not fit is noted and not applied, as ADR-0144 decides.

### 6. Downloads, and what the two grammars share

- **`alt=media`** serves the object as ADR-0110 decision 1 serves GET Object: `Content-Type`, `ETag` (the quoted
  MD5 hex, the XML API's, so a client that compares across grammars sees one), `Last-Modified`,
  `x-goog-generation`, `x-goog-metageneration`, `x-goog-hash`, `Accept-Ranges`, the stored header fields and
  `x-goog-meta-*`; one `Range` is `206`; `generation=` other than the current one is `404 notFound`.
  Without `alt=media` the answer is the resource, and a `Range` there is ignored.
- **One store, one index, one set of events**: a JSON write is a file every other file server serves; the
  exchange is recorded with scheme `gs` and the same `StoreChange` events (ADR-0124) as the XML API's, so a
  test that observes a write cannot tell which grammar made it, except by the command.

### 7. Uploads

- **Media** (`uploadType=media`): `name` is required and read from the query; the body is the object, with
  `Content-Type` of the request (default `application/octet-stream`). Stored as PUT Object stores
  (ADR-0110 decision 1.2), through the upload session.
- **Multipart** (`uploadType=multipart`): a `multipart/related` body (RFC 2387) of exactly two parts: first JSON
  object metadata, then the bytes. The boundary comes from the `Content-Type`; parts are read by hand with
  bounded buffers. A `name` in the metadata (or the query) names the object; the metadata's `contentType`
  wins over the second part's own. A third part, no boundary, or an unterminated body is `400 badRequest`. The
  metadata part is at most 1 MiB (`400 invalid` past it).
- **Resumable** (`uploadType=resumable`): the opening `POST` carries the JSON metadata (optional) and
  `X-Upload-Content-Type` and `X-Upload-Content-Length`; the answer is `200` with `Location` the session
  URI `/upload/storage/v1/b/<bucket>/o?uploadType=resumable&upload_id=<id>` as the request reached it
  (ADR-0110 amendment 2's rule). The chunk, status-check and completion rules are ADR-0110 amendment 2's
  for the XML API's resumable upload, answered as JSON: `308` with `Range`, `200` with `storage#object`.
  A cancel is **`499`** (the reference's JSON-tab answer, where the XML tab gives `204`). A session
  belongs to one grammar: an XML session ID used with a JSON URL, or the reverse, is `404 notFound`, and
  upload IDs are drawn as ADR-0110 amendment 2 draws them. The 1000 open uploads, the 24-hour idleness
  bound and the staging session are shared with the XML API (decision 8).
- **`patch`** applies the JSON body as a merge: a present field replaces it, `null` removes it, `metadata`
  merges by key (a `null` value removes the key). **`update`** replaces all writable metadata with the body's.
  Both raise the metageneration, leave the bytes and generation alone, rewrite the index entry (ADR-0110 amendment
  1's `Content-Type` etc.), and need `--allow-uploads`.
- **`copy`** and **`rewrite`** copy the bytes through the store to the destination (a new generation,
  metageneration `1`), taking the source's stored fields unless the body names new metadata. The source
  must be readable and the destination writable by the caller (share access, both). `rewrite` always
  finishes in one call: `done` is `true`, `totalBytesRewritten` and `objectSize` are the size, `resource` the
  object, and a `rewriteToken` or `maxBytesRewrittenPerCall` sent is ignored. A copy onto itself is a metadata
  copy with the new generation, as the page says. The source and destination may name different buckets.
- **`compose`** concatenates `sourceObjects` (1 to 32, in order, each readable, optionally with
  `generation` and `objectPreconditions.ifGenerationMatch`) into `destination`'s name, with the
  body's `destination` metadata (`contentType` default `application/octet-stream`). More than 32 sources
  is `400 invalid`; a missing source is `404 notFound`. The result is one new generation, and an
  `ifGenerationMatch` on the request applies to the destination.

### 8. Limits (ADR-0006), added to ADR-0110 decision 7

| Limit | How the JSON API applies it | Answer when hit |
| --- | --- | --- |
| `--max-filesize` | a media or multipart body's announced length and its bytes; a resumable total and each chunk; a composed or copied result | `413 uploadTooLarge`, then close |
| a JSON request body | 1 MiB (metadata, compose, patch, update) | `400 invalid` |
| a compose | 32 sources | `400 invalid` |
| a name | 1024 UTF-8 bytes | `400 invalid` |
| custom metadata | 8 KiB | `400 invalid` |
| a listing page | 1000 items | served as 1000 |
| open uploads and idle uploads | the XML API's 1000 and 24 hours, counted together | `503 backendError`; then `404 notFound` |
| `--max-request-head`, timeouts, connections | ADR-0110's | `400 invalid` for the head; the others as ADR-0110, with the JSON body |

No option is added.

### 9. The build

Each task is one library, `Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` and its
`.UnitTests`, and each keeps that project's quality gates:

| Task | Builds |
| --- | --- |
| BL-1045 | Decisions 1, 2 (buckets, objects list and get, delete, `alt=media`), 3 (resources, listing, metageneration in the index), 4, 5, 6 and the limits of 8 |
| BL-1041 | Decision 7's media and multipart insert, `patch` and `update` |
| BL-1042 | Decision 7's JSON resumable upload |
| BL-1043 | Decision 7's `copy`, `rewrite` and `compose` |
| BL-1044 | `gs`'s `--aihelp` about text, example and the matrix's last rows, after BL-1026 serves `gs` |

## Alternatives considered

- **Leaving the JSON API out.** Rejected: CLAUDE.md's default answer is a complete server, and the client
  libraries Surl's test-suite users run speak only the JSON API.
- **A separate scheme or listener** (`gsjson://`). Rejected: ADR-0110 and the task ask for one listener and one
  store, and a client given one `STORAGE_EMULATOR_HOST` may use either grammar.
- **Serving bucket creation and deletion** (`buckets.insert`, `buckets.delete`), which a test suite often
  calls first. Rejected for now, with ADR-0110 and ADR-0108: the bucket is a top-level folder every protocol shares,
  and one decision should make it for `s3`, `az` and `gs` together. It is the first thing to revisit, and it
  would add rows to this ADR and not change any.
- **Taking `access_token` in the query.** Rejected: ADR-0032's secure default, and RFC 6750 section 2.3 discourages it.
- **Accepting HMAC signatures on the JSON paths.** Rejected: Google does not, and a `GOOG4` header there is
  more likely a client pointed at the wrong endpoint.
- **Echoing Google's offending-parameter `location` fields.** Rejected: ADR-0006 section 3. The recorded exchange
  has the request whole.
- **`reflection-based` `System.Text.Json` serialisation** of the resources. Rejected: native AOT. `Utf8JsonWriter`
  needs no reflection and keeps member order exact.
- **`204` for a resumable cancel** (the XML tab's answer) on JSON. Rejected: the JSON tab gives `499`.

## Consequences

Good:

- A Google client library or `gcloud storage` pointed at `STORAGE_EMULATOR_HOST` can list, read, write, patch,
  copy and compose against the same files FTP, SFTP, HTTP, SMB, S3, Azure and the XML API serve, with
  the same accounts, shares, faults and events.
- The JSON and XML grammars read each other's objects, so a test can upload one way and verify the other.

Costs and caveats:

- Bucket creation and deletion are `501`; a test seeds its buckets.
- `fields`, `projection` and `prettyPrint` are ignored, so a partial-response request gets a whole resource.
- Three bucket names are reachable over the XML API by virtual-hosted style only (decision 1).
- BL-1045 to BL-1044 are all in one library, so they run one after another, and BL-1045 changes ADR-0100's index
  record (a metageneration), which the other file servers read and ignore.
- The matrix is `Documentation/Conformance/Extended/GoogleCloudStorage-Json.md` (ADR-0083 decision 9).
