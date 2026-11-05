# Surl.Extended.Protocol.S3.UnitLibrary

The `s3` server (ADR-0108): answers Amazon S3 REST requests over HTTP/1.1 on the shared
`ContentStore`, whose top-level folders are the buckets (ADR-0100 decision 2). An extended server
(ADR-0083): judged by the Amazon S3 API Reference, never by upstream curl (which has no `s3`) and
never by the Curl port. Namespace `Surl.Extended.Protocol.S3`.

What it serves now (BL-418): path-style GetObject and HeadObject with no query - one `Range`
(`206`, `416 InvalidRange`, several ranges ignored), the conditional fields with the GetObject
page's precedence (`412 PreconditionFailed`, `304`), the object's stored header fields and
`x-amz-meta-*` from `ObjectMetadataIndex` (the index's defaults, computed here, for a file in a
share or behind a `--bucket` name, which the index does not key). Every other request is
`501 NotImplemented`. Reads are anonymous until BL-420 adds the SigV4 check; BL-419 adds
PutObject, ListObjectsV2 and virtual-hosted addressing.

- `S3ProtocolServer` (public) - `IConnectionProtocolServer` over `Http1ConnectionServer`, and
  `IConnectionRefusalWriter` writing `503 SlowDown`.
- `S3RequestAnswerer` - the `IHttpRequestHandler`: operation, bucket, key length, key.
- `S3ObjectAnswer`, `S3Conditions`, `S3RangeSelection` - GetObject's head, conditions and range.
- `S3Reply`, `S3Error`, `S3ErrorBody` - every answer's `Date`, `Server` and `x-amz-request-id`
  (16 upper-case hexadecimal digits), and the one-line XML `Error` body; `Resource`, `Key` and
  `BucketName` echo the client's bytes as sent (still percent-encoded), printable ASCII only, cut
  to 1024 characters, then XML-escaped.

A head past `--max-request-head` is refused by `Surl.HttpServing` (`431`, ADR-0006 section 5),
not with ADR-0108's `400 RequestHeaderSectionTooLarge`, until `Surl.HttpServing` lets a handler
word its refusals.

This library references `Surl.Protocol.Abstractions`, `Surl.Content` and `Surl.HttpServing`
(and through it `Surl.HttpMessage`), nothing else (`ProtocolIsolationTests`). It never
constructs a transport type. Tests replay request bytes written from the API Reference through
`InMemoryConnection` on an `InMemoryContentFileSystem`.
