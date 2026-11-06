# Surl.Extended.Protocol.S3.UnitLibrary

The `s3` server (ADR-0108): answers Amazon S3 REST requests over HTTP/1.1 on the shared
`ContentStore`, whose top-level folders are the buckets (ADR-0100 decision 2). An extended server
(ADR-0083): judged by the Amazon S3 API Reference, never by upstream curl (which has no `s3`) and
never by the Curl port. Namespace `Surl.Extended.Protocol.S3`.

What it serves now (BL-418, BL-419, BL-712, BL-713), path style or virtual-hosted style:

- GetObject and HeadObject with no query - one `Range` (`206`, `416 InvalidRange`, several
  ranges ignored), the conditional fields with the GetObject page's precedence
  (`412 PreconditionFailed`, `304`), the object's stored header fields and `x-amz-meta-*`.
- PutObject (`PUT` with no query and no `x-amz-copy-source`): needs `--allow-uploads`, read from
  the store's `ContentExposureOptions` (off: `403 AccessDenied`, nothing read); checks
  `Content-MD5` (`400 InvalidDigest` malformed, `400 BadDigest` mismatched, old object kept),
  `x-amz-meta-*` at most 2 KiB (`400 MetadataTooLarge`), `Content-Length` (`411
  MissingContentLength`; chunked `400 InvalidRequest`, `aws-chunked` `501` until BL-711), the
  announced length against `--max-filesize` (`400 EntityTooLarge`, body left unread so the
  connection closes); creates the key's folders (a file in the way, or a folder at the key, is
  `409 InvalidArgument`; a key the store cannot hold `400 InvalidArgument` with `ArgumentName`
  `key`); streams the body through `S3UploadStream` into a content-store upload session
  (`400 IncompleteBody` when it ends early); answers `200` with the body's MD5 as `ETag`. A
  zero-byte `PUT` of a key ending in `/` creates the folder and records it as a folder object
  (`503 ServiceUnavailable` when the index is full). A write the store or a share
  refuses is `403 AccessDenied`.
- ListObjectsV2 (`GET /<bucket>?list-type=2`): a listing in ADR-0006's sense, so it needs
  `--list-directories` (off: `403 AccessDenied`). `S3BucketWalk` yields keys in UTF-8 binary
  order; `S3ListPage` applies `prefix`, `delimiter`, `start-after`, `max-keys` (default
  and cap 1000) and the continuation token (`S3ContinuationToken`: base64url of the last name);
  `S3ListObjectsV2` writes `ListBucketResult`. A folder is a key, `x/` (size 0, the empty-body ETag),
  only when a zero-byte PutObject of `x/` recorded it in the index as a folder object
  (`ObjectMetadataIndex.StoreFolderObjectAsync`; ADR-0108 Amendment 3, BL-728): the listing reads
  every folder object once and the walk yields `x/` just before the keys inside it. Every other
  folder - empty, made for a deeper key, made by another protocol, or in a share - is not a key.
  DeleteObject of `x/` does not yet forget the folder object (BL-928).
- DeleteObject (`DELETE /<bucket>/<key>`, `S3DeleteObject`): needs `--allow-uploads` (off: `403`
  `AccessDenied`) and a login; removes the file, or for a key ending in `/` the folder only when it
  is empty; `204` for every key that holds no object (missing, a folder without the `/`, a key the
  store cannot hold, a non-empty folder); a delete the store refuses (`readonly` or read-only share)
  is `403 AccessDenied`.
- HeadBucket (`HEAD /<bucket>`): `200` with no body; a missing bucket, a top-level file or a share
  the caller may not read is `404` with no body.
- ListBuckets (`GET /` with no query, `S3ListBuckets`): needs `--list-directories` (off: `403
  AccessDenied`); the root's top-level folders and shares the caller may read plus every `--bucket`
  that maps to a folder, in UTF-8 binary order, as `ListAllMyBucketsResult` (`CreationDate` the
  folder's last-write time; `Owner` the signed account or `anonymous`). ADR-0108 Amendment 2.
- Multipart uploads (BL-713; ADR-0108 decisions 1, 3 and 7), each needing `--allow-uploads` (off: `403
  AccessDenied`) and a login, a key over 1024 bytes `400 KeyTooLongError`. `S3OperationRecognition` reads an
  object request's query: `POST ?uploads` CreateMultipartUpload, `PUT ?partNumber&uploadId` UploadPart (not
  with `x-amz-copy-source`), `POST ?uploadId` CompleteMultipartUpload, `DELETE ?uploadId`
  AbortMultipartUpload; every other object query (ListParts too) is `501`.
  - `S3CreateMultipartUpload`: `x-amz-meta-*` past 2 KiB `400 MetadataTooLarge`; a key the store cannot hold
    or ending in `/` `400 InvalidArgument`; creates the key's folders (`409` as for PutObject); opens an
    upload session at the key that is never committed - a hidden `.surl-upload-<guid>` dot-file beside the
    object - and answers `InitiateMultipartUploadResult` with a 32-digit lower-case hex `UploadId`. The
    request's fields are kept for the completed object's metadata.
  - `S3UploadPart`: `partNumber` 1 to 10000 (else `400 InvalidArgument`, `ArgumentName` `partNumber`);
    PutObject's body checks; appends the body to the upload's session and records `S3UploadedPart` (offset,
    size, MD5), replacing a part of the same number (its old bytes stay staged); answers `200` with the
    part's MD5 as `ETag`. The staged bytes of one upload are held to `--max-filesize`: past it the upload is
    aborted and the answer is `400 EntityTooLarge`.
  - `S3CompleteMultipartUpload`: the body (at most 1 MiB, `S3BoundedBodyStream`) is checked against the
    signed hash and `Content-MD5`, then read by `S3CompleteMultipartUploadDocument` (`400 MalformedXML`:
    not a `CompleteMultipartUpload` of `Part`s each with one `PartNumber` and one `ETag`, no part, more than
    10000; `Checksum*` elements skipped); `400 InvalidPartOrder` (not strictly ascending), `400 InvalidPart`
    (not uploaded or another ETag; quotes optional, hex of either case), `400 EntityTooSmall` (a part but
    the last below 5 MiB). The parts are copied into a fresh session committed over the key; the
    metadata from CreateMultipartUpload is stored with ETag `"<hex MD5 of the parts' MD5s>-<count>"`; the
    answer is `CompleteMultipartUploadResult` (`Location` is `http(s)://<Host or listen host><path>`). A
    refused completion leaves the upload open.
  - `S3AbortMultipartUpload`: `204`, the staged bytes discarded.
  - `S3MultipartUploads` (one per `S3ProtocolServer`, shared by its connections): at most 1000 open (the
    1001st CreateMultipartUpload `503 SlowDown`); every multipart request runs one at a time under its gate
    and first aborts each upload untouched for more than 24 hours on the injected `TimeProvider`. An
    unknown `uploadId`, or one created for another bucket or key, is `404 NoSuchUpload` with `UploadId`.
  - Staging beside the object, not under ADR-0100's `.surl/objects/uploads/`, because `ContentStore`
    refuses every write under `/.surl` (the same choice BL-715 made for Azure blocks); ADR-0108's record of
    it is BL-927.
- Virtual-hosted addressing (`S3VirtualHost`): `<bucket>.<domain>` for the exchange's listen
  host when it is a DNS name, `localhost`, `s3.amazonaws.com` and `s3.<region>.amazonaws.com`.

- The SigV4 check (BL-420, ADR-0108 decision 4 and Amendment 1), before the operation: `S3Login`
  asks the connection's `IHttpAuthenticationSession` (from `IAuthenticationPolicy.StartHttpConnection`)
  with `AwsSigV4Service = "s3"` and maps each `HttpAuthenticationRefusal` to its error: no
  `Authorization` `403 AccessDenied`, another scheme `400 InvalidArgument` (`Unsupported
  Authorization Type`), malformed `400 AuthorizationHeaderMalformed`, no `x-amz-content-sha256`
  `400 InvalidRequest`, unknown key `403 InvalidAccessKeyId`, bad signature `403
  SignatureDoesNotMatch`, date over 15 minutes off `403 RequestTimeTooSkewed`, body not the signed
  hash `400 XAmzContentSHA256Mismatch`. Under `--allow-anonymous` an unsigned read is served as
  anonymous and an unsigned write is `403 AccessDenied`. The signed account is the `ContentCaller`
  of every store call (`S3LoginResult`). PutObject hashes the body with SHA-256 in `S3UploadStream`
  and asks the body check before `Content-MD5` and before committing; any other operation reads and
  discards a signed body first. Each checked login's note goes to the log.

Every other request is `501 NotImplemented`. Object properties (`S3ObjectPropertiesReader`) come from `ObjectMetadataIndex` under the
key `S3IndexKey` gives - the path under the served root, a `--bucket` name replaced by its
content path - and are computed here (MD5, default content type) for a file in a share, which the
index does not key; PutObject's metadata is kept only for files the index keys.

- `S3ProtocolServer` (public) - `IConnectionProtocolServer` over `Http1ConnectionServer`, and
  `IConnectionRefusalWriter` writing `503 SlowDown`.
- `S3RequestAnswerer` - the `IHttpRequestHandler`: addressing, login (`S3Login`), operation
  (`S3OperationRecognition`), bucket, then the operation.
- `S3PutObject`, `S3UploadStream`, `S3DeleteObject`; `S3CreateMultipartUpload`, `S3UploadPart`,
  `S3CompleteMultipartUpload`, `S3AbortMultipartUpload`, `S3MultipartUploads`, `S3MultipartUpload`,
  `S3UploadedPart`, `S3CompletedPart`, `S3CompleteMultipartUploadDocument`, `S3BoundedBodyStream`; `S3ListBuckets`; `S3ListObjectsV2`,
  `S3ListRequest`, `S3ListPage`, `S3BucketWalk`, `S3KeyOrder`, `S3ContinuationToken`;
  `S3QueryParameters`; `S3XmlElement` (one escaped element of a response body).
- `S3ObjectAnswer`, `S3Conditions`, `S3RangeSelection` - GetObject's head, conditions and range.
- `S3Reply`, `S3Error`, `S3ErrorBody` - every answer's `Date`, `Server` and `x-amz-request-id`
  (16 upper-case hexadecimal digits), and the one-line XML `Error` body; `Resource`, `Key`,
  `BucketName` and `ArgumentValue` echo the client's bytes as sent (still percent-encoded),
  printable ASCII only, cut to 1024 characters, then XML-escaped.

A head past `--max-request-head` is refused `400 RequestHeaderSectionTooLarge` with the `Error`
body (empty `Resource`), then closed (ADR-0108, decision 7; BL-720): `S3RequestAnswerer` is also
`Surl.HttpServing`'s `IHttpHeadNotReadRefuser`. Every other head that could not be read keeps
`Surl.HttpServing`'s standard refusal (`505`, `408`, `400`, each with an empty body).

This library references `Surl.Protocol.Abstractions`, `Surl.Content` and `Surl.HttpServing`
(and through it `Surl.HttpMessage`), nothing else (`ProtocolIsolationTests`). It never
constructs a transport type. Tests replay request bytes written from the API Reference through
`InMemoryConnection` on an `InMemoryContentFileSystem`, judged by the hand-written
`UnitTestS3AuthenticationPolicy` (by default an unsigned request logs in as
`AKIAIOSFODNN7EXAMPLE`, so the operation tests need not sign); the signature arithmetic itself is
tested in `Surl.Authentication.UnitTests` (`AwsSigV4ServiceSignatureTests`) against the API
Reference's worked GET-object example.
