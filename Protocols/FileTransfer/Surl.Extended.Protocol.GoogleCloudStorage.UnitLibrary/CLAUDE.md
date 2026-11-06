# Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary

The `gs` server (ADR-0110): answers Google Cloud Storage XML API requests over HTTP/1.1 on the shared
`ContentStore`, whose top-level folders are the buckets (ADR-0100 decision 2). An extended server (ADR-0083):
judged by the Cloud Storage XML API pages, never by upstream curl (which has no `gs`) and never by the Curl
port. Namespace `Surl.Extended.Protocol.GoogleCloudStorage`. Its requirement matrix is
`Documentation/Conformance/Extended/GoogleCloudStorage.md`.

What it serves now (BL-431), path style or virtual-hosted style (`GcsVirtualHost`: `<bucket>.<domain>` for the
listen URL's host when it is a DNS name, `localhost` and `storage.googleapis.com`):

- GET Object and HEAD Object (`?generation=` the current one only, any other `404 NoSuchKey`) - one `Range`
  (`206`, `416 InvalidRange`), the HTTP conditional fields (`GcsConditions`), `x-goog-if-generation-match` and
  `x-goog-if-metageneration-match` (`GcsGenerationConditions`), the stored header fields, `x-goog-meta-*`,
  `x-goog-generation`, `x-goog-metageneration: 1`, `x-goog-stored-content-*`, `x-goog-storage-class: STANDARD`
  and `x-goog-hash` (`crc32c=…,md5=…` of the whole object, computed by `GcsObjectHashes` with
  `BitOperations.Crc32C`, read from the file on every request until BL-773 caches it in the index).
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

Every other request - another method, a subresource or unlisted query parameter, a copy, a POST,
`x-goog-encryption-*` - is `400 NotImplemented`, judged after the login. Every error is `GcsError`'s fixed
code and message in the one-line XML body (`GcsErrorBody`); no client byte is echoed, and a `HEAD` error has
no body. A connection past a limit is `503 SlowDown` (`WriteRefusalAsync`). A head past `--max-request-head`
is still `Surl.HttpServing`'s `431` until BL-774.

This library references `Surl.Protocol.Abstractions`, `Surl.Content` and `Surl.HttpServing` (and through it
`Surl.HttpMessage`), nothing else (`ProtocolIsolationTests`). It never constructs a transport type. Tests
replay request bytes written from the XML API pages through `InMemoryConnection` on an
`InMemoryContentFileSystem`, judged by the hand-written `UnitTestGcsAuthenticationPolicy`.
