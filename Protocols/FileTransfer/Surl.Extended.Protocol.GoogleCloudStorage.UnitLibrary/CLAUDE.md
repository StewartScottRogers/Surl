# Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary

The `gs` server (ADR-0110, ADR-0148): answers Google Cloud Storage XML API requests - and, on the JSON paths, the
JSON API's reads and delete (BL-1045, below) - over HTTP/1.1 on the shared
`ContentStore`, whose top-level folders are the buckets (ADR-0100 decision 2). An extended server (ADR-0083):
judged by the Cloud Storage XML API pages, never by upstream curl (which has no `gs`) and never by the Curl
port. Namespace `Surl.Extended.Protocol.GoogleCloudStorage`. Its requirement matrix is
`Documentation/Conformance/Extended/GoogleCloudStorage.md`.

What it serves now (BL-431), path style or virtual-hosted style (`GcsVirtualHost`: `<bucket>.<domain>` for the
listen URL's host when it is a DNS name, `localhost` and `storage.googleapis.com`):

- GET Object and HEAD Object (`?generation=` the current one only, any other `404 NoSuchKey`) - one `Range`
  (`206`, `416 InvalidRange`), the HTTP conditional fields (`GcsConditions`), `x-goog-if-generation-match` and
  `x-goog-if-metageneration-match` (`GcsGenerationConditions`), the stored header fields, `x-goog-meta-*`,
  `x-goog-generation`, `x-goog-metageneration` (the index's recorded value, 1 unless a JSON patch or update raised it,
BL-1041), `x-goog-stored-content-*`, `x-goog-storage-class: STANDARD`
  and `x-goog-hash` (`crc32c=…,md5=…` of the whole object, computed by `GcsObjectHashes` with
  `BitOperations.Crc32C`, cached in the index entry beside the ETag since BL-773: `GcsObjectDigests.Cached` builds it from the cached CRC-32C and the MD5 a quoted-hex ETag names, and `GcsObjectPropertiesReader.DigestsAsync` reads the file only when there is no cached CRC-32C or the ETag is a multipart one).
- PUT Object (`GcsPutObject`): needs `--allow-uploads` (`403 AccessDenied`), `Content-Length` (`411
  MissingContentLength`), checks `Content-MD5` (`400 InvalidDigest`, `400 BadDigest`) and `x-goog-hash`
  (`400 InvalidHash`, `400 BadDigest`, `GcsSentDigests`), `x-goog-meta-*` at most 8 KiB (`400
  MetadataTooLarge`), the announced length against `--max-filesize` (`400 EntityTooLarge`); creates the name's
  folders (`409 InvalidObjectName` for a file in the way, `400 InvalidObjectName` for a name the store cannot
  hold), streams the body through `GcsHashingStream` into an upload session (`400 IncompleteBody`), and keeps
  the metadata and the upload time as the generation in `ObjectMetadataIndex` (full: `403 InsufficientQuota`).
  A zero-byte PUT of a name ending in `/` creates the folder.
- DELETE Object: `204`; missing `404 NoSuchKey`; uploads off or share access refusing `403 AccessDenied`.
- List Objects (`GcsListObjects`, `GcsListRequest`, `GcsListPage`, `GcsBucketWalk`): version 1 (`marker`,
  `NextMarker`) and version 2 (`list-type=2`, `continuation-token`, `start-after`, `fetch-owner`), `prefix`,
  `delimiter`, `encoding-type=url`, `max-keys` (default and cap 1000), in UTF-8 byte order. A listing needs
  `--list-directories` (off: `403 AccessDenied`).
- List Buckets (`GcsListBuckets`, `GET /`): each top-level folder or `--bucket` name the caller may read.
- Logins (`GcsLoginJudge`): `Authorization: Bearer` through the connection's `IHttpAuthenticationSession`;
  another scheme `400 InvalidAuthentication`; no credential (anonymous off) or an anonymous write `401
  AuthenticationRequired` with `WWW-Authenticate: Bearer realm="storage"`; a refused token `401
  InvalidAuthentication` with `error="invalid_token"`.
- V4 HMAC signatures (BL-432, ADR-0110 decisions 3.3 and 3.4): an `Authorization` whose first token is
  `GOOG4-HMAC-SHA256` (any case) is judged by the same session as a request for the `storage` service
  (`GcsLoginJudge.SignedService`), which picks `Surl.Authentication`'s `GoogleV4HmacAuthenticationMethod`;
  the library never references it. Each `HttpAuthenticationRefusal` is decision 3.4's error
  (`GcsLoginJudge.ErrorOf`): malformed `400 InvalidArgument`, no date or content hash `400
  MissingSecurityHeader`, unknown access ID `403 InvalidAccessKeyId`, bad signature `403
  SignatureDoesNotMatch`, skewed `403 RequestTimeTooSkewed`, body mismatch `400 BadDigest`; no challenge
  is sent. A hex `x-goog-content-sha256` binds the body once read (ADR-0045): PUT Object hashes it on its
  way into the upload (`GcsHashingStream`'s SHA-256) and checks it before `Content-MD5` and `x-goog-hash`
  and before committing; every other request reads and discards it first (`JudgeDiscardedBodyAsync`).
  Tests replay BL-430's hand-worked GET and a signed PUT worked the same way (`GcsSignedRequests`).

- Multipart and resumable uploads (BL-718, ADR-0110 amendment 2), recognised by method and the exact set of
  query names (`GcsOperationRecognition`): `GcsMultipartUpload` (initiate `?uploads`, part
  `?partNumber&uploadId` 1-10000, list parts `?uploadId` with `max-parts` and `part-number-marker`, abort),
  `GcsCompleteMultipartUpload` (body read by `GcsCompleteMultipartUploadDocument`; `InvalidPartOrder`,
  `InvalidPart`, `EntityTooSmall` below 5 MiB, ETag `"<md5 of md5s>-<n>"`) and `GcsResumableUpload` (`POST`
  with `x-goog-resumable: start` is `201` with a `?upload_id=` session URI; `PUT` chunks by `Content-Range`,
  read by `GcsContentRange`, `308 Resume Incomplete` with `Range` until the total is in, then `200`; cancel
  `204`). Both share `GcsUploadStaging` (opening, appending a body, committing with the opening request's
  metadata) and the server-wide `GcsStagedUploads` (1000 open, 24-hour idle discard on `TimeProvider`, one
  request at a time): each upload is a `GcsStagedUpload` whose bytes sit in an uncommitted upload session at
  the object, invisible until completion. An unknown, finished or other-form ID is `404 NoSuchUpload`.

- Chosen-error faults (BL-982, ADR-0124 decisions 4 and 9; `GcsRequestFaults`): once a login proceeds and
  before the operation or the store, each request asks `ExchangeContext.Faults` at the command stage through
  `HttpCommandFaults` - `command` the method, `path` the path-style, percent-decoded `/`, `/<bucket>` or
  `/<bucket>/<name>`, `account` the login. An `error` rule whose reply is a status 400-599 and a code word
  (`503 SlowDown`) is answered with that status and code in the `Error` body (the server's own message for a
  code it sends, else `GcsRequestFaults.ChosenErrorMessage`) and publishes one `FaultAppliedEvent`; a reply
  that does not fit, or a delay or drop rule at a request, is noted and the request is served.

- The JSON API (BL-1045, ADR-0148 decisions 1 to 6 and the limits of 8; matrix
  `Documentation/Conformance/Extended/GoogleCloudStorage-Json.md`). `GcsRequestAnswerer` hands a path-style request
  whose path begins `/storage/v1/`, `/upload/storage/v1/` or `/download/storage/v1/` (`GcsJsonRoute.IsJsonPath`) to
  `GcsJsonRequestAnswerer` before any login; a virtual-hosted request is always the XML API. `GcsJsonRoute` recognises
  `buckets.list` (`project` required), `buckets.get`, `objects.list` (`prefix`, `delimiter`, `startOffset`, `endOffset`,
  `maxResults`, `pageToken`; `includeTrailingDelimiter` is validated and changes nothing, no file name ending in the
  delimiter being listed), `objects.get` (resource, or bytes for `alt=media` and the download path; `generation`,
  `if*Match`, `If-*`, one `Range`; `GcsJsonObjectQuery`, `GcsJsonConditions`) and `objects.delete`, by method, path
  and the exact query parameters (`%2F` and `/` both separate folders); anything else, `HEAD` included,
  is `501 notImplemented` after the login. `GcsLoginJudge(isJson: true)` takes Bearer only (a
  `GOOG4-HMAC-SHA256` header is `400 invalid`) and refuses in `GcsJsonError` rows (`401 required`, `401 authError`,
  `400 invalid`). Resources are written by `GcsJsonResources` with `Utf8JsonWriter` (`GcsJson`, member order as
  ADR-0148 decision 3, `etag` the base64 of `<generation>/<metageneration>`, `timeCreated` the generation as a
  time); errors by `GcsJsonErrorBody`, one line, fixed text. A chosen fault (`GcsJsonFaults`) asks the plan with the
  Google method id as command (`storage.objects.get`) and the percent-decoded path; its status picks the row, else
  `backendError`. The metageneration is `ObjectMetadata.Metageneration` in `ObjectMetadataIndex` (BL-1045); a
  connection refused past a limit is still the XML `503 SlowDown`, the grammar not being known before a request.

- JSON writes (BL-1041, ADR-0148 decision 7). `GcsJsonObjectInsert` answers `POST /upload/storage/v1/b/<bucket>/o`:
  `uploadType=media` (`name` in the query, required; the body is the object, `Content-Type` of the request or
  `application/octet-stream`) and `uploadType=multipart` (`GcsMultipartRelatedSink` reads the `multipart/related` body by
  hand: exactly two parts, the first JSON metadata held up to 1 MiB, the second streamed into an upload session; the
  metadata's `name` wins over the query's, its `contentType` over the second part's own). Both need `--allow-uploads`
  (`403 forbidden`), `Content-Length` (`411 lengthRequired`), a length within `--max-filesize` (`413 uploadTooLarge`);
  check the conditions against the object there now (`GcsJsonConditions.AreMetByWrite`: `ifGenerationMatch=0` means none
  exists; `412 conditionNotMet`), `Content-MD5`, `x-goog-hash` and the metadata's `md5Hash` and `crc32c` (`400 invalid`,
  nothing stored), create the name's folders (`409 conflict`) and commit the session only when all pass; a name ending in
  `/` or past 1024 bytes is `400 invalid`. The object is kept in the index as PUT Object keeps it
  (`GcsPutObject.StoreMetadataAsync`, metageneration 1; a full index is `403 quotaExceeded`). `GcsJsonObjectUpdate` answers
  `PATCH` (merge; a `null` removes a member or a `metadata` key) and `PUT` (replace) of `.../o/<object>`: the object found and
  its conditions met as for a delete, a JSON body of at most 1 MiB (`400 invalid`), writable metadata of at most 8 KiB, the
  index entry rewritten with the metageneration plus one and the ETag, generation and bytes left alone; a file in a share
  has no index entry, so it is `403 forbidden`. A malformed `multipart/related` body (no boundary, a third part, a close
  after one part, no close, bytes before the first delimiter, part fields past 8 KiB) is `400 badRequest`; metadata past
  1 MiB `400 invalid`.

- JSON resumable uploads (BL-1042, ADR-0148 decision 7 and amendment 2). `GcsJsonResumableUpload` answers `POST /upload/storage/v1/b/<bucket>/o?uploadType=resumable`
  (`200`, `Location` the session URI `?uploadType=resumable&upload_id=<id>`; the metadata body optional, `X-Upload-Content-Type`
  and `X-Upload-Content-Length` read; conditions, name and metadata judged at the opening), `PUT` of the session URI (a chunk by
  `Content-Range`, read by `GcsContentRange`: `308` with `Range` until the total is in, then `200` and the `storage#object`; `bytes */<total>`
  with no body is a status check) and `DELETE` (`499`). The staged bytes, the 1000-open and 24-hour bounds are the XML API's
  (`GcsUploadStaging`, `GcsStagedUploads`); a session is `GcsUploadKind.JsonResumable`, found by ID and bucket
  (`GcsStagedUploads.FindJson`), so an XML ID on a JSON URL, or the reverse, is `404 notFound`. `GcsStagedUpload.Metadata` holds the
  opening's `ObjectMetadata`, which `GcsUploadStaging.CommitAsync` stores in place of the XML opening's fields.

- JSON copy, rewrite and compose (BL-1043, ADR-0148 decision 7 and amendment 3). `GcsJsonRoute` recognises `POST …/o/<object>/copyTo/b/<bucket>/o/<object>`, `…/rewriteTo/b/…` (route members `RawDestinationBucket`, `RawDestinationName`) and `…/o/<object>/compose`; `GcsJsonObjectCopy` answers copy and rewrite (source read, `sourceGeneration` and `ifSource…` judged, body metadata optional, `storage#object` or the finished `storage#rewriteResponse`; `rewriteToken` ignored) and `GcsJsonObjectCompose` compose (`GcsJsonComposeRequest` reads 1 to 32 sources and the destination's fields). Both write through `GcsJsonObjectAssembly`: name, `--max-filesize` on the sources' total, destination conditions, folders, then the sources' bytes through the hashes into an upload session committed last and kept in the index with metageneration 1.

Every other request - another method, a subresource or unlisted query parameter, an XML API copy, a `POST` that is no
upload, `x-goog-encryption-*` - is `400 NotImplemented`, judged after the login. Every error is `GcsError`'s fixed
code and message in the one-line XML body (`GcsErrorBody`); no client byte is echoed, and a `HEAD` error has
no body. A connection past a limit is `503 SlowDown` (`WriteRefusalAsync`). A head past `--max-request-head`
is `400 MaxMessageLengthExceeded` with the `Error` body, then closed (ADR-0110 decision 7; BL-774):
`GcsRequestAnswerer` is also `Surl.HttpServing`'s `IHttpHeadNotReadRefuser`, and every other head that
could not be read keeps the standard refusal.

This library references `Surl.Protocol.Abstractions`, `Surl.Content` and `Surl.HttpServing` (and through it
`Surl.HttpMessage`), nothing else (`ProtocolIsolationTests`). It never constructs a transport type. Tests
replay request bytes written from the XML API pages through `InMemoryConnection` on an
`InMemoryContentFileSystem`, judged by the hand-written `UnitTestGcsAuthenticationPolicy`.
