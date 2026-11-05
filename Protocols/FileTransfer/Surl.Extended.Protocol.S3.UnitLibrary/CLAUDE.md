# Surl.Extended.Protocol.S3.UnitLibrary

The `s3` server (ADR-0108): answers Amazon S3 REST requests over HTTP/1.1 on the shared
`ContentStore`, whose top-level folders are the buckets (ADR-0100 decision 2). An extended server
(ADR-0083): judged by the Amazon S3 API Reference, never by upstream curl (which has no `s3`) and
never by the Curl port. Namespace `Surl.Extended.Protocol.S3`.

What it serves now (BL-418, BL-419), path style or virtual-hosted style:

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
  zero-byte `PUT` of a key ending in `/` creates the folder. A write the store or a share
  refuses is `403 AccessDenied`.
- ListObjectsV2 (`GET /<bucket>?list-type=2`): a listing in ADR-0006's sense, so it needs
  `--list-directories` (off: `403 AccessDenied`). `S3BucketWalk` yields keys in UTF-8 binary
  order; `S3ListPage` applies `prefix`, `delimiter`, `start-after`, `max-keys` (default
  and cap 1000) and the continuation token (`S3ContinuationToken`: base64url of the last name);
  `S3ListObjectsV2` writes `ListBucketResult`. Empty folders are not keys.
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
- `S3PutObject`, `S3UploadStream`; `S3ListObjectsV2`, `S3ListRequest`, `S3ListPage`,
  `S3BucketWalk`, `S3KeyOrder`, `S3ContinuationToken`; `S3QueryParameters`.
- `S3ObjectAnswer`, `S3Conditions`, `S3RangeSelection` - GetObject's head, conditions and range.
- `S3Reply`, `S3Error`, `S3ErrorBody` - every answer's `Date`, `Server` and `x-amz-request-id`
  (16 upper-case hexadecimal digits), and the one-line XML `Error` body; `Resource`, `Key`,
  `BucketName` and `ArgumentValue` echo the client's bytes as sent (still percent-encoded),
  printable ASCII only, cut to 1024 characters, then XML-escaped.

A head past `--max-request-head` is refused by `Surl.HttpServing` (`431`, ADR-0006 section 5),
not with ADR-0108's `400 RequestHeaderSectionTooLarge`, until `Surl.HttpServing` lets a handler
word its refusals.

This library references `Surl.Protocol.Abstractions`, `Surl.Content` and `Surl.HttpServing`
(and through it `Surl.HttpMessage`), nothing else (`ProtocolIsolationTests`). It never
constructs a transport type. Tests replay request bytes written from the API Reference through
`InMemoryConnection` on an `InMemoryContentFileSystem`, judged by the hand-written
`UnitTestS3AuthenticationPolicy` (by default an unsigned request logs in as
`AKIAIOSFODNN7EXAMPLE`, so the operation tests need not sign); the signature arithmetic itself is
tested in `Surl.Authentication.UnitTests` (`AwsSigV4ServiceSignatureTests`) against the API
Reference's worked GET-object example.
