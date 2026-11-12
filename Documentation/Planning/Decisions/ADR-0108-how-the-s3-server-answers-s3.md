# ADR-0108 — How the S3 server answers `s3`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-417. BL-418 builds the server with GetObject and HeadObject (decisions 1, 2, 3, 8 and the
  error body), BL-419 PutObject, ListObjectsV2 and virtual-hosted addressing (decisions 1, 2, 3 and
  6), BL-420 the SigV4 check (decision 4), BL-712 DeleteObject, HeadBucket and ListBuckets (decision
  1), BL-713 the multipart uploads (decisions 1 and 7), BL-711 presigned URLs, `aws-chunked` bodies
  and checksum headers (decision 4), and BL-421 serves `s3` from `surl` with its TLS and its option
  (decision 5).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof),
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decisions 1, 4 and 5 (`Surl.HttpServing`, logins through `IHttpAuthenticationSession`, objects on
  `Surl.Content`), [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decisions 2 and 4.1 (the bucket mapping, refused keys, the object metadata index, the shared
  accounts), [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decisions 2.2 to 2.4 (share access and `--bucket`), [ADR-0043](ADR-0043-http-aws-signature-version-4-check.md)
  and [ADR-0045](ADR-0045-an-aws-signature-version-4-body-is-bound-after-it-is-read.md) (the SigV4
  check), [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) (secure by
  default) and [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits, fixed error text).
- **Amends:** nothing. Decision 4.4 names a field BL-420 adds to `HttpAuthenticationVerdict` in
  `Surl.Protocol.Abstractions`; the HTTP server ignores it, so no HTTP byte changes.

## Context

`Surl.Extended.Protocol.S3` (in `Protocols/FileTransfer/`, ADR-0083's table) answers the `s3` scheme
the Curl port adds, on port 443 (ADR-0083 decision 7). By ADR-0083 decision 1 it is judged by the
Amazon S3 API Reference, not by upstream curl, which has no `s3` scheme, and not by the Curl port.
The pages read (2026-11-05) and relied on, cited below by title:

- **API Reference** (<https://docs.aws.amazon.com/AmazonS3/latest/API/>): "GetObject", "HeadObject",
  "PutObject", "DeleteObject", "ListObjectsV2", "ListBuckets", "HeadBucket",
  "CreateMultipartUpload", "UploadPart", "CompleteMultipartUpload", "AbortMultipartUpload",
  "Error responses" (the `Error` body and "List of error codes"), "Common Request Headers",
  "Common Response Headers", "Authenticating Requests (AWS Signature Version 4)", "Signature
  Calculations for the Authorization Header: Transferring Payload in a Single Chunk", "...:
  Transferring Payload in Multiple Chunks (Chunked Upload)", and "Authenticating Requests: Using
  Query Parameters (AWS Signature Version 4)".
- **User Guide** (<https://docs.aws.amazon.com/AmazonS3/latest/userguide/>): "Virtual hosting of
  buckets", "Bucket naming rules", "Object key naming guidelines", "Working with object metadata"
  and "Checking object integrity".

Which requests a real client sends, as background and not as a verdict: the port's
`Curl.Extended.Protocol.S3.UnitLibrary` sends virtual-hosted requests to `s3.<region>.amazonaws.com`
and path-style ones to a custom endpoint (`AWS_ENDPOINT_URL`, plain HTTP allowed), GetObject,
PutObject, ListObjectsV2 with continuation tokens, and the three multipart requests for large
uploads, each signed with SigV4 in the `Authorization` header over a single-chunk payload. AWS's own
SDKs and CLI also send presigned URLs, `aws-chunked` bodies and `x-amz-checksum-*` headers, and so
the server answers them (decision 4.5).

## Decision

### 1. The operations served, and the one answer for any other

An operation is recognised by method, by whether the path names a bucket and a key, and by its
query subresources, as the API Reference's "Request Syntax" of each page writes it:

| Operation | Request | Success | Page | Task |
| --- | --- | --- | --- | --- |
| ListBuckets | `GET /` on the service | `200`, `ListAllMyBucketsResult` | ListBuckets | BL-712 |
| HeadBucket | `HEAD /<bucket>` | `200`, no body | HeadBucket | BL-712 |
| ListObjectsV2 | `GET /<bucket>?list-type=2` | `200`, `ListBucketResult` (decision 6) | ListObjectsV2 | BL-419 |
| GetObject | `GET /<bucket>/<key>` | `200`, or `206` for a range | GetObject | BL-418 |
| HeadObject | `HEAD /<bucket>/<key>` | GetObject's head, no body | HeadObject | BL-418 |
| PutObject | `PUT /<bucket>/<key>` | `200`, `ETag`, no body | PutObject | BL-419 |
| DeleteObject | `DELETE /<bucket>/<key>` | `204`, also for a key that does not exist | DeleteObject | BL-712 |
| CreateMultipartUpload | `POST /<bucket>/<key>?uploads` | `200`, `InitiateMultipartUploadResult` | CreateMultipartUpload | BL-713 |
| UploadPart | `PUT /<bucket>/<key>?partNumber=<n>&uploadId=<id>` | `200`, the part's `ETag` | UploadPart | BL-713 |
| CompleteMultipartUpload | `POST /<bucket>/<key>?uploadId=<id>` | `200`, `CompleteMultipartUploadResult` | CompleteMultipartUpload | BL-713 |
| AbortMultipartUpload | `DELETE /<bucket>/<key>?uploadId=<id>` | `204` | AbortMultipartUpload | BL-713 |

- **Every other request** - another method, another subresource (`?acl`, `?tagging`, `?versioning`,
  `?location`, `?cors`, `?delete`, `?uploads` on a bucket, `?versionId`, `?attributes`, …), a
  ListObjects version 1 (`GET /<bucket>` without `list-type=2`), CreateBucket, DeleteBucket,
  CopyObject (a `PUT` carrying `x-amz-copy-source`), `OPTIONS`, a `POST` form upload - is answered
  **`501 Not Implemented`, `<Code>NotImplemented</Code>`**, Message `A header you provided implies
  functionality that is not implemented` ("List of error codes": `NotImplemented`, 501). It is
  judged after authentication (decision 4), so an unauthenticated peer learns nothing about which
  operations exist.
- **Buckets are not created or deleted over S3.** A bucket is a top-level folder (decision 3), made
  by `--directory`'s contents, by another protocol (FTP `MKD`, SFTP `mkdir`, SMB) or by `--bucket`
  (ADR-0102 decision 2.4). CreateBucket and DeleteBucket are `NotImplemented`: an object store test
  harness seeds its buckets, and letting a peer remove a top-level folder every file server serves
  is a larger hazard than the S3 page's convenience.
- **ListObjects version 1 is `NotImplemented`.** The ListObjects page itself tells clients to use
  ListObjectsV2, and every client in view does; one listing grammar keeps one paging code path.
- **Conditional and range headers** on GetObject and HeadObject follow the GetObject page:
  `If-Match` failing is `412 PreconditionFailed`, `If-None-Match` matching `304 Not Modified`,
  `If-Modified-Since` not modified `304`, `If-Unmodified-Since` failing `412`, with the page's
  precedence (`If-Match` true and `If-Unmodified-Since` false serves; `If-None-Match` false and
  `If-Modified-Since` true is `304`). One `Range: bytes=` range is `206` with `Content-Range`;
  an unsatisfiable one is **`416`, `<Code>InvalidRange</Code>`**; several ranges, or a syntax the
  page does not give, are ignored and the whole object served with `200` (GetObject: "Amazon S3
  doesn't support retrieving multiple ranges of data per GET request"). `partNumber` on GetObject
  is `NotImplemented`.
- **Response headers.** Every answer carries `x-amz-request-id` ("Common Response Headers"): 16
  upper-case hexadecimal digits from `RandomNumberGenerator`, the same value as the error body's
  `RequestId`. No `x-amz-id-2` is sent (it is AWS's internal host identifier) and no `Server` field
  beyond what `Surl.HttpServing` writes for every server (ADR-0006 section 3). GetObject and
  HeadObject also carry `Content-Length`, `Content-Type`, `ETag`, `Last-Modified` (IMF-fixdate,
  the file's last-write time), `Accept-Ranges: bytes`, each stored `Content-Encoding`,
  `Content-Disposition`, `Cache-Control` and `Content-Language`, and each stored `x-amz-meta-*`
  (ADR-0100 decision 2.3; defaults for a file with no metadata entry).

#### 1.1 The error body ("Error responses")

```
<?xml version="1.0" encoding="UTF-8"?>
<Error><Code>NoSuchKey</Code><Message>The specified key does not exist.</Message><Resource>/photos/2026/a.jpg</Resource><RequestId>4442587FB7D0A2F9</RequestId></Error>
```

- `Content-Type: application/xml`, the body as one line with no whitespace between elements (the
  page's example is indented for reading; XML ignores it either way), UTF-8.
- `Code` and `Message` are fixed per code, from the "List of error codes" table's description
  (ADR-0006 section 3: fixed text). `Resource` is the request's path as the client sent it, still
  percent-encoded, XML-escaped (`&`, `<`, `>`) and cut to 1024 bytes; it is the only client byte
  echoed. Codes that the page lists with an extra element (`Key` for `NoSuchKey`, `BucketName` for
  `NoSuchBucket`, `ArgumentName` and `ArgumentValue` for `InvalidArgument`) carry it, escaped the
  same way; `SignatureDoesNotMatch`'s `StringToSign` and `CanonicalRequest` elements are **not**
  sent, because they disclose how the server canonicalised the request to a peer who may be probing
  it.
- A `HEAD` error has the status and `x-amz-request-id` and no body (HeadObject: "the response to a
  HEAD request has no body").

The codes the server sends, each with the status the "List of error codes" table gives it:

| Code | Status | When |
| --- | --- | --- |
| `AccessDenied` | 403 | No signature with anonymous off; an anonymous write; a write while `--allow-uploads` is off; a write a share's access forbids (ADR-0102 decision 2.3); an expired presigned URL |
| `AuthorizationHeaderMalformed` | 400 | An `AWS4-HMAC-SHA256` header that does not parse, or whose scope does not end `/s3/aws4_request` |
| `BadDigest` | 400 | `Content-MD5` or an `x-amz-checksum-*` value not matching the body |
| `EntityTooLarge` | 400 | A body, a part or a completed upload past `--max-filesize` (decision 7) |
| `EntityTooSmall` | 400 | A multipart part other than the last below 5 MiB, at completion |
| `IncompleteBody` | 400 | A PutObject body that ends before the length it announced, or whose framing is malformed (Amendment 2) |
| `InvalidAccessKeyId` | 403 | A signature whose access key is no account |
| `InvalidArgument` | 400 | A bad query value (`max-keys`, `continuation-token`, `partNumber`, `X-Amz-Expires`); an `Authorization` scheme other than SigV4; an unmappable key on a write; and `409` for a key that conflicts with a folder or file (ADR-0100 decision 2.2) |
| `InvalidDigest` | 400 | A `Content-MD5` that is not the base64 of 16 bytes |
| `InvalidPart`, `InvalidPartOrder`, `NoSuchUpload` | 400, 400, 404 | CompleteMultipartUpload's own checks (BL-713) |
| `InvalidRange` | 416 | An unsatisfiable range |
| `InvalidRequest` | 400 | A signed request without `x-amz-content-sha256`; a `Transfer-Encoding: chunked` body with no `x-amz-decoded-content-length` |
| `KeyTooLongError` | 400 | A key longer than 1024 UTF-8 bytes ("Object key naming guidelines") |
| `MalformedXML` | 400 | A CompleteMultipartUpload body that does not parse |
| `MetadataTooLarge` | 400 | `x-amz-meta-*` names and values past 2 KiB ("Working with object metadata"), or ADR-0100's 8 KiB per-object bound |
| `MissingContentLength` | 411 | A `PUT` with neither `Content-Length` nor a decoded length |
| `NoSuchBucket` | 404 | The bucket's folder does not exist, or the caller may not read its share (ADR-0102 decision 2.3) |
| `NoSuchKey` | 404 | No file at the key, the key is a folder, or the key is unmappable (ADR-0100 decision 2.2) |
| `NotImplemented` | 501 | Decision 1's every other request; an `aws-chunked` variant decision 4.5 does not serve |
| `PreconditionFailed` | 412 | A failed `If-Match` or `If-Unmodified-Since` |
| `RequestHeaderSectionTooLarge` | 400 | A request head past `--max-request-head` (decision 7) |
| `RequestTimeTooSkewed` | 403 | `x-amz-date` (or `X-Amz-Date`) more than 15 minutes from the server's clock |
| `ServiceUnavailable` | 503 | ADR-0100's object metadata index is full |
| `SignatureDoesNotMatch` | 403 | A signature, or an `aws-chunked` chunk signature, that does not verify |
| `SlowDown` | 503 | A connection past `--max-connections` or `--max-connections-per-address` (decision 7) |
| `XAmzContentSHA256Mismatch` | 400 | A body whose SHA-256 is not `x-amz-content-sha256` |

### 2. Path-style and virtual-hosted addressing ("Virtual hosting of buckets")

- **Path style** is always served: the path's first segment is the bucket, the rest the key
  (`/photos/2026/a.jpg`); `/` alone is the service (ListBuckets).
- **Virtual-hosted style** is served when the `Host` field's name (port removed, case folded,
  without a trailing dot) is `<bucket>.<domain>` for one of the server's **virtual-hosting
  domains**; the whole path is then the key and `/` is the bucket (ListObjectsV2, HeadBucket).
  `<bucket>` is everything before the domain, dots included (a bucket name may hold dots, "Bucket
  naming rules").
- **How the listener knows its own host name.** The virtual-hosting domains are, in this order:
  1. the listen URL's host, when it is a DNS name and not an IP literal or a wildcard (`surl
     s3://s3.test:9000` serves `photos.s3.test:9000`); the composition passes it to the server's
     constructor, never read from a static;
  2. `localhost` (RFC 6761 section 6.3: `photos.localhost` resolves to loopback on common systems
     with no setup);
  3. AWS's own endpoint forms, `s3.amazonaws.com` and `s3.<region>.amazonaws.com` for any region
     label, so a client pointed at Surl by `--resolve`, `--connect-to` or a hosts file sends its
     unmodified virtual-hosted requests. A `Host` naming `s3.amazonaws.com` or `s3.<region>…`
     itself is path style.
  No option is added: a harness that wants another domain names it as the listen URL's host.
- **Any other `Host`** - an IP literal, a name matching no domain, none at all - is path style.
  Region is never checked against anything: the server is every region at once, so no
  `PermanentRedirect` is ever sent.
- Over TLS, a virtual-hosted name with dots cannot match a wildcard certificate; `--self-signed`'s
  certificate names the listen host and `localhost` only, so a client using dotted buckets goes
  path style, as the user guide advises. The server does not refuse it.

### 3. Buckets and keys on the content store (ADR-0100 decision 2, applied)

- **Bucket = top-level folder** of the one shared `ContentStore`, resolved through
  `IContentShareLookup` (ADR-0100 decision 2.6), so `--bucket <name>=<path>` (ADR-0102 decision
  2.4, BL-663) needs no S3 change. A top-level file is not a bucket; `.surl` is never one.
- **Key `x/y/z` = file `z` in folder `x/y`** under the bucket's folder; the key is percent-decoded
  once from the path (S3 encodes it once, "Signature Calculations…": `UriEncode` the key without
  encoding `/`), as UTF-8. A zero-byte PutObject whose key ends in `/` creates the folder; a
  non-empty one is refused.
- **Keys the store cannot hold** (ADR-0100 decision 2.2's list - an empty segment, `.`, `..`, `\`,
  `:`, a control character, a trailing dot or space, a reserved device name): a write is **`400
  InvalidArgument`**, `ArgumentName` `key`; a key whose first segment would shadow an entry of the
  other kind is **`409 InvalidArgument`**; a read of either is `404 NoSuchKey`. A key over 1024
  UTF-8 bytes is `400 KeyTooLongError` on every operation.
- **Metadata and ETags** are ADR-0100 decision 2.3's index: PutObject stores `Content-Type`,
  `Content-Encoding`, `Content-Disposition`, `Cache-Control`, `Content-Language` and each
  `x-amz-meta-*` (names lower-cased, as S3 returns them); the ETag is the quoted lower-case hex MD5
  of the body for a single-part upload and `"<hex MD5 of the parts' MD5s>-<part count>"` for a
  multipart one ("Checking object integrity"). A file another protocol wrote is served with the
  index's defaults.
- **Share access** (ADR-0102 decision 2.3): a bucket or key the caller may not read is `404
  NoSuchBucket` or `NoSuchKey`; a write the caller may not make is `403 AccessDenied`. Every store
  call names the caller (the SigV4 account, or anonymous) and the scheme `s3`.
- **Multipart staging** is beside the object, not under ADR-0100's `.surl/objects/uploads/`
  (Amendment 7, point 1): each upload's parts sit one after another in a content-store upload
  session opened at the key at CreateMultipartUpload and never committed - a hidden
  `.surl-upload-<guid>` dot-file in the key's folder. The upload ID is 32 lower-case hexadecimal
  digits from `RandomNumberGenerator`. Parts are invisible to every protocol until
  CompleteMultipartUpload copies the named parts into a fresh upload session and commits it over
  the key.

### 4. Authentication

#### 4.1 What is required

- **By default every request carries a valid SigV4 signature** of an account in the one account set
  (`--user`, `--user-file`; ADR-0100 decision 4.1: name = access key ID, password = secret access
  key). A request with no `Authorization` header and no `X-Amz-Signature` query is **`403
  AccessDenied`**.
- **`--allow-anonymous`** serves unsigned ListBuckets, HeadBucket, ListObjectsV2, GetObject and
  HeadObject. An unsigned write (PutObject, DeleteObject, the multipart requests) is still `403
  AccessDenied`. A signed request is checked even then; a bad signature is not served as anonymous.
- **Writes** need `--allow-uploads` (ADR-0006 section 2) **and** a SigV4 login; with uploads off, a
  signed write is `403 AccessDenied` (ADR-0102 decision 2.3's `NotPermitted`).
- **Order:** request head and limits, then addressing, then authentication, then the operation, the
  bucket, the share access and the key, so a peer without a login learns only `AccessDenied` or
  the signature's own refusals.

#### 4.2 The `Authorization` header form ("Authenticating Requests (AWS Signature Version 4)")

Checked by `Surl.Authentication`'s existing SigV4 method (ADR-0043, ADR-0045) through
`IHttpAuthenticationSession`, with the service fixed to `s3`: the canonical URI encoded once,
the query re-encoded and sorted, the signed fields, the payload hash from `x-amz-content-sha256`
(the hex SHA-256, `UNSIGNED-PAYLOAD`, or a decision 4.5 streaming value), the date within 15
minutes either way on the injected `TimeProvider`. Any region in the scope is accepted. A body with
a hex hash is bound once read (ADR-0045): a mismatch is `400 XAmzContentSHA256Mismatch` and the
object is not stored. `x-amz-security-token` is accepted only as a signed field and grants nothing:
there is no token service, and the signature with the account's secret is the proof.

#### 4.3 Each refusal

| Failure | Status, code |
| --- | --- |
| No signature, anonymous off; or an anonymous write | `403 AccessDenied` |
| `Authorization` with another scheme (Basic, Bearer, Digest, AWS signature version 2 `AWS <key>:<sig>`) | `400 InvalidArgument`, Message `Unsupported Authorization Type` |
| `AWS4-HMAC-SHA256` that does not parse, or a scope whose service is not `s3` | `400 AuthorizationHeaderMalformed` |
| Signed without `x-amz-content-sha256` | `400 InvalidRequest`, Message `Missing required header for this request: x-amz-content-sha256` |
| Access key that is no account | `403 InvalidAccessKeyId` |
| Signature that does not verify | `403 SignatureDoesNotMatch` |
| `x-amz-date` more than 15 minutes off | `403 RequestTimeTooSkewed` |
| Body not hashing to `x-amz-content-sha256` | `400 XAmzContentSHA256Mismatch` |
| Presigned URL past `X-Amz-Date` + `X-Amz-Expires` (`SignatureExpired`) | `403 AccessDenied`, Message `Request has expired` |
| Presigned URL with an `Authorization` header too (`ConflictingCredentials`) | `400 InvalidArgument`, Message `Only one auth mechanism allowed; only the X-Amz-Algorithm query parameter, Signature query string parameter or the Authorization header should be specified` |
| `X-Amz-Expires` not a number of seconds from 1 to 604800 (`ExpiryOutOfRange`) | `400 InvalidArgument`, Message `X-Amz-Expires must be a number of seconds from 1 to 604800` |

- **`InvalidAccessKeyId` is sent, as S3 sends it.** ADR-0043's HTTP check answers an unknown key as
  a bad signature, against a random secret, so an HTTP peer cannot tell which names exist. S3's
  access key ID is an identifier the S3 page itself reveals as invalid, and BL-420's acceptance and
  every S3 client's diagnostics expect the code; the S3 server follows the specification. The check
  still runs against a random secret, so both answers take the same time, and the HTTP server's
  answer does not change.
- **Each refusal waits the failed-login delay** the authentication session already applies
  (ADR-0032), then answers; the connection stays open as S3's does.
- **How this differs from the HTTP server.** The HTTP server answers a missing or failed login
  `401` with `WWW-Authenticate` challenges, Digest first (ADR-0032 section 4). The S3 server never
  sends `401` or `WWW-Authenticate`: S3 has no challenge, every refusal is the `403` or `400` above
  with the XML body, and `--auth` does not apply to `s3` (SigV4 is its only method).

#### 4.4 The contract BL-420 adds

`HttpAuthenticationVerdict` says only `Proceed`, `Challenge` or `Forbidden`, which cannot tell
`InvalidAccessKeyId` from `SignatureDoesNotMatch` or `RequestTimeTooSkewed`. BL-420 adds to it an
optional reason (planning name `HttpAuthenticationRefusal`: `MissingCredentials`,
`UnsupportedScheme`, `MalformedCredentials`, `MissingPayloadHash`, `UnknownAccount`,
`BadSignature`, `ClockSkew`, `Expired`) in `Surl.Protocol.Abstractions`, set by
`Surl.Authentication`'s SigV4 method, and an `IHttpRequestBodyCheck` outcome distinguishing a hash
mismatch. The HTTP server ignores both, so its bytes do not change; the S3 server maps them through
decision 4.3's table. The server references neither `Surl.Authentication` nor an account
(ADR-0084 decision 4). BL-420's `touches` gains both libraries.

#### 4.5 Presigned URLs, `aws-chunked` and checksums (BL-711)

- **Presigned query strings are served** ("Authenticating Requests: Using Query Parameters"):
  `X-Amz-Algorithm`, `X-Amz-Credential`, `X-Amz-Date`, `X-Amz-Expires` (1 to 604800 seconds, else
  `400 InvalidArgument`), `X-Amz-SignedHeaders`, `X-Amz-Signature`, the payload `UNSIGNED-PAYLOAD`,
  the canonical query without `X-Amz-Signature`. Expired is `403 AccessDenied`. A request with both
  a query signature and an `Authorization` header is `400 InvalidArgument`.
- **`aws-chunked` bodies are served** for `x-amz-content-sha256: STREAMING-AWS4-HMAC-SHA256-PAYLOAD`
  (each chunk's signature chained from the seed signature, "Transferring Payload in Multiple
  Chunks"; a bad chunk signature `403 SignatureDoesNotMatch`) and
  `STREAMING-UNSIGNED-PAYLOAD-TRAILER` (its trailing checksum checked). `x-amz-decoded-content-length`
  is the object's length; missing it is `411 MissingContentLength`. Other streaming values
  (`STREAMING-AWS4-ECDSA-P256-SHA256-PAYLOAD`, the signed `-TRAILER` forms) are `501
  NotImplemented`.
- **Checksums** ("Checking object integrity"): `Content-MD5` is checked when sent (`BadDigest`,
  or `InvalidDigest` when malformed); `x-amz-checksum-crc32`, `-crc32c`, `-sha1` and `-sha256` are
  checked when sent (`400 BadDigest`) and echoed on PutObject's answer; `crc64nvme` is `501
  NotImplemented`. CRC-32 and CRC-32C are written by hand where the BCL has none
  (`System.IO.Hashing` is a package, not the BCL).

### 5. TLS

- **`s3` is HTTPS**: implicit TLS from the first byte, as every S3 endpoint is, wrapped in
  `ImplicitTlsSchemeServer` as `https` is (ADR-0010), on port 443 (ADR-0083 decision 7). TLS 1.2
  and 1.3 (ADR-0006 section 4); `--cert` or `--self-signed` is required, `CertificateProblem` (58)
  without, as ADR-0032 decides for every secure listen URL. ALPN offers `http/1.1` only: the server
  answers HTTP/1.1 through `Surl.HttpServing`, and a client offering `h2` alone is not agreed.
- **The option that changes it: `--s3-plaintext`** (no argument, category `s3`, off by default,
  warned on start as ADR-0032's loosening options are) serves `s3` as plain HTTP/1.1, for the
  custom-endpoint clients that test against `http://` (`AWS_ENDPOINT_URL=http://…`). SigV4 sends no
  secret, so `--allow-plaintext-auth` is not needed with it; presigned URLs and signatures are
  still checked. BL-421 adds the option with its `OptionArgumentType`, help category and
  `--aihelp` facts.

### 6. ListObjectsV2 ("ListObjectsV2")

- **Namespace** `http://s3.amazonaws.com/doc/2006-03-01/` on `ListBucketResult`, as on
  `ListAllMyBucketsResult`, `InitiateMultipartUploadResult` and `CompleteMultipartUploadResult`
  (each page's example).
- **Elements**, in the page's order: `Name`, `Prefix`, `Delimiter` (when given), `MaxKeys`,
  `KeyCount`, `IsTruncated`, `ContinuationToken` (when given), `NextContinuationToken` (when
  truncated), `StartAfter` (when given), `EncodingType` (when given), each `Contents` (`Key`,
  `LastModified` as `yyyy-MM-ddTHH:mm:ss.fffZ`, `ETag`, `Size`, `StorageClass` `STANDARD`, and
  `Owner` with `ID` and `DisplayName` only when `fetch-owner=true`, both the account name, or
  `anonymous`), then each `CommonPrefixes` `Prefix`.
- **Order:** keys in ascending UTF-8 byte order (the page: "returned in UTF-8 binary order"),
  which a walk of the folder tree yields when each folder's entries are sorted by their UTF-8
  bytes and a folder `x` is visited as the prefix `x/`.
- **`max-keys`**: default 1000, values above 1000 served as 1000, `0` gives an empty untruncated
  page; a negative or non-numeric value is `400 InvalidArgument`. `KeyCount` counts `Contents` and
  `CommonPrefixes` together, and `max-keys` bounds that sum.
- **`prefix`** limits to keys beginning with it; **`delimiter`** (any string, not only `/`) rolls
  keys sharing a prefix up to the first delimiter after `prefix` into one `CommonPrefixes`, listed
  once; **`start-after`** begins after that key; **`encoding-type=url`** percent-encodes `Key`,
  `Prefix`, `Delimiter`, `StartAfter` and each common prefix, and any other value is `400
  InvalidArgument`.
- **`continuation-token`** is opaque to the client: Surl's is the base64url (no padding) of the
  UTF-8 bytes of the last key or common prefix returned, so a listing resumes correctly across
  writes. A token that does not decode is `400 InvalidArgument`, Message `The continuation token
  provided is incorrect`. Given with `start-after`, the token wins (the page's rule).
- **Empty folders** are not keys: a folder holds no object unless a client put `x/` (decision 3),
  which the index records; such a folder object is listed as key `x/` with size 0.

### 7. Limits (ADR-0006)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| `--max-request-head` (100 KiB) | `Surl.HttpServing`'s head reader | `400 RequestHeaderSectionTooLarge`, then close |
| `--max-filesize` (100 MiB) | a PutObject body, each UploadPart body and the completed multipart object; checked against `Content-Length` (or the decoded length) before the body is read, and again while it is written | `400 EntityTooLarge`, then close (the body is not drained) |
| the largest object | `--max-filesize`, single-part or multipart alike: an "uploaded file" in ADR-0006's words. S3's own 5 GiB single-PUT and 5 TiB object caps are above any default and need no second bound | as above |
| multipart parts | `partNumber` 1 to 10000 ("UploadPart"), else `400 InvalidArgument`; every part but the last at least 5 MiB (`EntityTooSmall` at completion); 1000 open uploads per server, the 1001st CreateMultipartUpload `503 SlowDown`; an upload untouched for 24 hours is aborted and its parts removed, timed on `TimeProvider` | per row |
| a key | 1024 UTF-8 bytes | `400 KeyTooLongError` |
| user metadata | 2 KiB of `x-amz-meta-*` names and values; ADR-0100's 8 KiB a object and 100000 entries | `400 MetadataTooLarge`; `503 ServiceUnavailable` |
| a CompleteMultipartUpload body | 10000 parts and 1 MiB | `400 MalformedXML` |
| a listing page | 1000 keys and common prefixes (decision 6) | served as 1000 |
| `--head-timeout` (30 s), `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.HttpServing`'s and `Surl.Core`'s, as for HTTP | close with no bytes |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s, through the server's refusal writer | `503 SlowDown` with the XML body, then close (ADR-0006 section 5: the protocol's own error) |

No other new option is added; the fixed bounds are guards and ADR-0006's options cover what a test
harness tunes.

### 8. `--aihelp`

The topic is `s3` (ADR-0083 decision 8). Its about text says the server answers Amazon S3 REST
requests on the shared content store, is judged by the Amazon S3 API Reference and not by upstream
curl, requires SigV4 by default, and is HTTPS unless `--s3-plaintext`. BL-421 writes it with its
example.

## Alternatives considered

- **A plain-HTTP `s3` by default.** Rejected: every S3 endpoint is HTTPS and ADR-0032 makes a
  secure listen URL the default; the option keeps the custom-endpoint case one flag away.
- **A second scheme for plain HTTP (`s3+http`).** Rejected: ADR-0083's table fixes `s3` alone, and
  the Curl port has no such scheme to mate.
- **Answering an unknown access key as `SignatureDoesNotMatch`,** as the HTTP check does. Rejected
  for S3 (decision 4.3); kept for HTTP.
- **Serving CreateBucket and DeleteBucket.** Rejected (decision 1): a top-level folder is shared by
  every file server.
- **Serving ListObjects version 1 beside version 2.** Rejected (decision 1): a second paging grammar
  for a request no current client sends.
- **Sending `StringToSign` and `CanonicalRequest` in `SignatureDoesNotMatch`,** as S3 does.
  Rejected: a debugging aid that tells a probing peer how the request was canonicalised; the client
  already knows what it signed.
- **Leaving presigned URLs and `aws-chunked` out.** Rejected: AWS's SDKs and CLI send them, and the
  standing rule leaves nothing out because it is work.

## Consequences

Good:

- The S3 server answers every request the port's client sends and the common SDK forms besides, on
  the same files, accounts and shares as FTP, SFTP, HTTP and SMB.
- Every refusal is one fixed row with its S3 code, so tests pin each byte from the API Reference.

Costs and caveats:

- BL-420 changes `Surl.Protocol.Abstractions` and `Surl.Authentication`, so it runs apart from every
  protocol task (the board's `touches` rule).
- CRC-32 and CRC-32C are hand-written in BL-711.
- The matrix is `Documentation/Conformance/Extended/S3.md` (ADR-0083 decision 9).

## Amendment 1 - the contract decision 4.4 planned, as BL-420 built it (BL-420, 2026-11-05)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in
BL-420.

1. **`HttpAuthenticationRequest.AwsSigV4Service`** (`null` by default) marks a request whose only
   method is Signature Version 4 for that service; the S3 server sets it to `s3`.
   `Surl.Authentication`'s session then judges by decision 4.1 rather than by `--auth`: no
   `Authorization` is anonymous under `--allow-anonymous` and `Forbidden` otherwise, another scheme
   is `Forbidden` unchecked, a signature is checked even under `--allow-anonymous` (with a SigV4
   method the policy builds over the account set, whatever `--auth` accepts), the scope's service
   must be `s3`, `x-amz-content-sha256` is required, and every refusal is `Forbidden` with no
   challenge. Why: decision 4.1's rules differ from the HTTP server's at every step, so the
   request has to say which rules apply; the S3 server still references no account and no method.
2. **`HttpAuthenticationRefusal`** has `MissingCredentials`, `UnsupportedScheme`,
   `MalformedCredentials`, `MissingPayloadHash`, `UnknownAccount`, `BadSignature`, `ClockSkew` and
   `PayloadHashMismatch`, on `HttpAuthenticationVerdict.Refusal`. `PayloadHashMismatch` is how a
   body check says "hash mismatch" (decision 4.4's "`IHttpRequestBodyCheck` outcome"), so the body
   check keeps its one method and verdict type. `Expired` is left to BL-711, which serves presigned
   URLs: a member nothing sets would say something untrue of the code.
3. **A head whose signature is verified names its account while the body is awaited.** With a
   sent content hash the signature is checked on the head, so the verdict carrying the
   `IHttpRequestBodyCheck` now carries the account too. The S3 server needs it before the body, to
   refuse an anonymous write and to map the key for the caller. The HTTP, WebSocket and RTSP
   servers take the account from the body's verdict, so none of their bytes change.
4. **Missing credentials and another scheme are answered at once;** only a checked signature waits
   the refusal delay (decision 4.3's "each refusal waits" applies to what was checked, as ADR-0032
   section 8 has it for HTTP).
5. **A signed body the operation does not read** (a `GET` with a body, an operation answered `501`)
   is read and hashed before the answer, so a body that is not the one signed is
   `XAmzContentSHA256Mismatch` there too. PutObject checks the hash before `Content-MD5` and before
   committing, so a mismatched object is never stored.

## Amendment 2 - PutObject, ListObjectsV2 and virtual hosting, as BL-419 built them (BL-727, 2026-11-06)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in
BL-419, and recorded here by BL-727 (BL-419 could not edit this folder while BL-463 held it).

1. **ListObjectsV2 needs `--list-directories`;** off, it is **`403 AccessDenied`**
   (`S3ListObjectsV2`). Why: a bucket listing is a directory listing in ADR-0006's sense, and
   every other file server lists only when that option is on; secure by default, loosened for a
   test by one option (ADR-0032).
2. **`--allow-uploads` and `--max-filesize` are read from the `ContentStore`'s
   `ExposureOptions`** (`AllowUploads`, `MaxUploadBytes`), the store the server's constructor
   receives. Why: the store already carries them for every file server, so there is no static and
   no second constructor parameter that could disagree with the store.
3. **The listen host for virtual hosting (decision 2's first domain) is read per exchange from
   `ExchangeContext.ListenUrl.Host`** (`S3RequestAnswerer`, `S3VirtualHost`), not passed to the
   constructor as decision 2 planned. Why: it is the same value, already on every exchange, and
   reading it there needs no change to the composition.
4. **A PutObject body that ends before its announced length, or whose framing is malformed, is
   `400 IncompleteBody`** ("List of error codes": `IncompleteBody`, 400; Message `You did not
   provide the number of bytes specified by the Content-Length HTTP header.`), and the upload
   session is not committed, so the old object stands. Why: it is S3's own code for exactly this
   case; decision 1.1's table now lists it. When BL-419 built it, `Surl.HttpServing` dropped a
   refusal's body, so `IncompleteBody` and `EntityTooLarge` were ordinary error answers that closed
   the connection after the lingering drain. BL-729 made both refusals that keep their XML body and
   close without the drain (Amendment 8).
5. **A `PUT` with `Transfer-Encoding: chunked` and no `x-amz-decoded-content-length` is `400
   InvalidRequest`;** one with it is `aws-chunked`, answered `501 NotImplemented` until BL-711
   serves decision 4.5. Why: S3 has no plain chunked upload, and a chunked body without a decoded
   length gives no object length to check against `--max-filesize` before reading. BL-711 changed
   the second half: a chunked body with a decoded length but no `STREAMING-` hash is now `400
   InvalidRequest` too, not `501`, because it is not `aws-chunked` (Amendment 9, point 10).
6. **The object metadata index keys an object by its path under the served root, with a
   `--bucket` name replaced by that bucket's content path** (`S3IndexKey`); **a file in a share has
   no index key,** so PutObject keeps no metadata for it and GetObject and HeadObject answer the
   computed defaults (MD5 ETag, default content type). Why: ADR-0100 decision 2.3's index covers
   the served root and a share lies outside it; keying a `--bucket` object by its content path makes
   S3 and every other protocol see one entry for one file. This also corrected BL-418's lookup of a
   `--bucket` object, which had used the bucket name as the key.
7. **A PutObject whose metadata the index refuses removes the file just written** and answers `400
   MetadataTooLarge` (past ADR-0100's 8 KiB per object) or `503 ServiceUnavailable` (the index
   full). Why: an object S3 says was not stored must not stand without the metadata its client
   sent.
8. **PutObject creates the key's missing folders;** a file in the way, or a folder at the key, is
   `409 InvalidArgument` (decision 3's conflict). A zero-byte `PUT` of a key ending in `/` creates
   the folder, and since BL-728 records it as a folder object that ListObjectsV2 lists (decision
   6's last point; Amendment 3).

## Amendment 3 - folder objects, as BL-728 built them (BL-728, 2026-11-06)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in
BL-728. It builds decision 6's last point: a folder a client put as `x/` is listed as key `x/`.

1. **The object metadata index records a folder object** (`ObjectMetadataIndex.StoreFolderObjectAsync`,
   in `Surl.Content`), under the folder's index key and a final `/` (`bucket/x/`), which
   `ObjectMetadataIndex.IsValidFolderObjectKey` accepts and the index file's format now admits
   beside file keys. Its properties: length 0, the ETag `"d41d8cd98f00b204e9800998ecf8427e"` (the
   MD5 of no bytes), the header fields and `x-amz-meta-*` the PUT sent, and the folder's last write
   time when it was stored. Why the index and not a marker the S3 library keeps: decision 6 already
   names the index, the index is what a store snapshot, reset and restore (ADR-0124 decision 3) and
   the data directory's persistence already carry, so a folder object is reset, restored and kept
   across restarts with no new mechanism; a dot-file marker in the folder would have made the folder
   non-empty, so FTP's `RMD` or SFTP's `rmdir` of an S3-made empty folder would fail.
2. **A folder object lasts while its folder exists,** whatever is written inside it - not while a
   stamp matches, as a file's entry does, because a folder's last write time moves with every entry
   added to it. `ObjectMetadataIndex.ReadFolderObjectsAsync` answers each folder object whose folder
   is still a folder and drops the rest; a full index drops them first, as it drops stale file
   entries. The stated cost, like ADR-0100's for renames: a folder removed and made again by another
   protocol before any listing looks is listed again as a folder object.
3. **ListObjectsV2 reads every folder object once per listing,** and `S3BucketWalk` yields a folder
   `x` as the key `x/` just before the keys inside it (its UTF-8 order) when the index holds it and
   the key starts with `prefix`; `delimiter` rolls it into its common prefix like any key. Any other
   folder - empty or not, made by S3's own folder creation for a deeper key or by any other protocol
   - is not a key. Why read them all at once: the walk is synchronous and paged by
   `S3ListPage`, and a folder that turned out not to be an object after paging would have been
   counted against `max-keys` or rolled into a common prefix it should not make.
4. **A folder put as an object in a share** has no index key (Amendment 2, point 6): the PUT still
   creates the folder and answers `200`, but nothing is recorded and ListObjectsV2 does not list it.
   A folder object the index refuses (the index full) is `503 ServiceUnavailable`; the folder stays,
   as a folder, because it may already hold other keys.
5. **DeleteObject of `x/` does not forget the folder object;** it removes the folder only when empty,
   and an empty folder's removal drops the folder object at the next listing. A non-empty folder's
   `x/` still lists after its DeleteObject: BL-930 makes DeleteObject forget it.

## Amendment 4 - DeleteObject, HeadBucket and ListBuckets as BL-712 built them (BL-918, 2026-11-11)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in
BL-712, and recorded here by BL-918 (BL-712 could not edit this folder while BL-687 held it).
`S3OperationRecognition` recognises the three operations; `S3DeleteObject`, `S3ListBuckets` and
`S3XmlElement` (the XML escaping `S3ListObjectsV2` already used, now shared) serve them.

1. **Recognition.** ListBuckets is `GET /` path style with no query; any query (`prefix`,
   `max-buckets`, `continuation-token`, `bucket-region`) and any other method on `/` is `501
   NotImplemented`. HeadBucket is `HEAD /<bucket>` with no query; DeleteObject is
   `DELETE /<bucket>/<key>` with no query (`?versionId` stays `501`). Why: an unfiltered list
   answering a filtered request would be untrue, and the server keeps no versions.
2. **ListBuckets needs `--list-directories`;** off, it is **`403 AccessDenied`**, as ListObjectsV2
   is (Amendment 2, point 1). Why: a list of buckets is a listing in ADR-0006's sense; secure by
   default. `S3RequestAnswerer` answers it before the bucket check. The buckets are every name the
   root listing gives the caller (shares included) plus every `--bucket`, kept when it maps for the
   caller to a folder - a top-level file, `.surl`, a share the caller may not read and a `--bucket`
   whose folder is missing are left out - once each, in UTF-8 binary order.
3. **The body** follows the API Reference page's example order: `Buckets`, with each `Bucket`'s
   `CreationDate` then `Name`, then `Owner` with `DisplayName` then `ID` (the signed account, or
   `anonymous`, as ListObjectsV2's `fetch-owner` names it), in the 2006-03-01 namespace
   `ListBucketResult` uses. `CreationDate` is the folder's last write time in `LastModified`'s form,
   because the store keeps no folder creation time.
4. **HeadBucket** is `200` with the common fields and no body; a missing bucket, a top-level file or
   a share the caller may not read is `404` with no body (decision 1's rule for every `HEAD` error).
5. **DeleteObject** checks `--allow-uploads` first (off: `403 AccessDenied`); an unsigned `DELETE`
   is refused by `S3Login` as every unsigned write is, even with `--allow-anonymous`; then the
   bucket, then `KeyTooLongError`. A file key goes to the store's `DeleteFile`; a key ending in `/`
   to `RemoveEmptyDirectory`, so a non-empty folder stays. Every outcome but `NotPermitted` is
   **`204 No Content`** - no entry, a folder named without the `/`, a key that does not map, a
   non-empty folder - as S3 answers a delete of a missing key; `NotPermitted` (a read-only file or a
   read-only share) is `403 AccessDenied`.
6. **The object metadata index needs no removal on DeleteObject:** the deleted file's entry keeps a
   stamp that no longer matches any file, so it reads as absent and a later PutObject of the same key
   replaces it. A folder object is the exception Amendment 3, point 5 states.

## Amendment 5 - `--s3-plaintext` as BL-1021 built it (BL-1021, 2026-11-11)

Decided by Claude under Stewart's delegation. Decision 5's option, as built:

1. **Where it acts.** `Surl.Composition`'s `S3PlaintextComposition.MarkS3ListenUrlsPlain` sets
   `ListenUrl.IsImplicitTls` to `false` on every `s3` listen URL at the start of
   `SurlServerComposition.ComposeAsync` (BL-1020's member), and `ServerTlsComposition` reads that
   per-listen-URL answer instead of `TlsSchemes`. So the `(58)` refusal and the TLS settings both
   follow the listen URL: a plain `s3` needs no certificate and builds no TLS settings, and a
   certificate given beside it is not read for it. The engine's handshake and its refusal writer
   follow the same answer (BL-1020). Why: one place decides, and every host (`surl`, `Surl.Api`,
   `Surl.Mcp`) composes through it.
2. **The default port stays 443.** `s3://host/` without a port listens on 443 with or without the
   option (`ExtendedSchemeDefaultPorts`); a test names its port, usually 0. Why: the listen URL is
   read before the option's effect is known in a `-K` file's order, and the parser's default ports
   are per scheme; a second default for one flag would make `s3://host/` mean two ports.
3. **Categories `s3`, `security` and `testing`,** off by default and negatable, with an `Explanation`
   as every loosening option has. The start warning, written with the other loosening warnings
   from the info level up, is `surl: warning: --s3-plaintext: s3 is served as plain HTTP, without TLS`.
   It is written whether or not an `s3` listen URL is given, as `--allow-plaintext-auth`'s is.

## Amendment 6 - serving `s3` from `surl`, as BL-421 built it (BL-421, BL-861, 2026-11-11)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-421; recorded
here by BL-861, because BL-421 could not edit this folder while another task held it. It builds
decision 5 except `--s3-plaintext`.

1. **`s3` is an implicit-TLS scheme.** It joins `TlsSchemes`'s implicit-TLS set in
   `Surl.Protocol.Abstractions`, and `Surl.Networking`'s `TlsApplicationProtocols` offers
   `http/1.1` alone for it (decision 5: the server answers HTTP/1.1 only). The engine handshakes
   before the S3 server sees the connection, so the `(58)` refusal of a TLS scheme without `--cert`
   or `--self-signed` applies unchanged, and `S3ProtocolServer` is registered directly through
   `Surl.Composition`'s `ExtendedProtocolServers.Compose`, with no `ImplicitTlsSchemeServer` around
   it. Why: the engine already secures every implicit-TLS scheme's connection, and
   `ImplicitTlsSchemeServer` exists to reuse a plaintext scheme's server under a TLS scheme name;
   `s3` has no plaintext scheme to reuse, so the server answers `s3` itself.
2. **The one `ObjectMetadataIndex` of the served root is service state:**
   `ServiceState.ObjectMetadataIndex`, built by `SurlServerComposition.ComposeObjectMetadataIndex`
   (persisted in `<path>/.surl/objects/index` with `--directory`, held in memory without it) and
   loaded after the LDAP directory by `LoadObjectMetadataIndexAsync`. An unreadable or malformed
   index ends the start with `(37) Could not read <file>: <reason>`, as an unreadable LDAP
   directory or DNS zone does. `ExtendedProtocolServerServices` carries it, with the clock, to the
   S3 server. Why: ADR-0100 decision 2.3 names one index per served root, shared by every protocol
   that reads the store; building it once per start keeps it one.
3. **`--s3-plaintext` (decision 5) is left to BL-860,** because the engine decides TLS by scheme
   alone and a per-option switch from TLS to plaintext needs a seam it does not yet have. Until
   then `s3` is always HTTPS. (Amendment 5 records `--s3-plaintext` as BL-1021 built it.)
4. **The `s3` help category** ("Amazon S3 protocol (extended)") is listed by `--directory`,
   `--allow-uploads`, `--list-directories`, `--head-timeout`, `--max-request-head`,
   `--max-filesize`, `-u`, `--user-file` and `--allow-anonymous`, and the manual has an
   `S3 OPTIONS` section.

## Amendment 7 - multipart uploads, as BL-713 built them (BL-927, 2026-11-11)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in
BL-713, and recorded here by BL-927. The code is `S3CreateMultipartUpload`, `S3UploadPart`,
`S3CompleteMultipartUpload`, `S3AbortMultipartUpload` and `S3MultipartUploads` in
`Surl.Extended.Protocol.S3.UnitLibrary`.

1. **Parts are staged beside the object, not under `.surl/objects/uploads/<upload id>/`.** Each
   upload's parts sit one after another in a `ContentUploadSession` opened at the key at
   CreateMultipartUpload and never committed: a hidden `.surl-upload-<guid>` dot-file beside the
   object, invisible to every protocol, deleted on abort, on expiry or at completion.
   CompleteMultipartUpload copies the named parts, in order, into a fresh session and commits it.
   Why: `ContentStore` refuses every write under `/.surl`, so decision 3's path cannot be written
   through the store, and the upload session already gives an uncommitted, invisible file that the
   store cleans up; BL-715 made the same choice for Azure blocks (ADR-0109, BL-922).
2. **The key's folders are created at CreateMultipartUpload,** and the key's mapping, the `409
   InvalidArgument` conflict and `MetadataTooLarge` are checked there. Why: the staging file sits in
   those folders, so they must exist before the first part; refusing a bad key at the start spares a
   client uploading every part of an object that can never be stored.
3. **The staged bytes of one upload together are held to `--max-filesize`** (the session's own
   limit): past it the upload is aborted and the answer is `400 EntityTooLarge`. Why: the completed
   object is an "uploaded file" in ADR-0006's words (decision 7), and its bytes are on disk from the
   first part, so the bound must hold while staging rather than only at completion.
4. **A part uploaded again replaces the old one;** the old bytes stay staged until the upload ends.
   Why: S3 says a part number uploaded again overwrites the part ("UploadPart"); leaving the old
   bytes in the append-only session keeps staging a single sequential write, and they still count
   against point 3's bound, so a client cannot grow the file past it by repeating parts.
5. **Multipart requests run one at a time per server** (`S3MultipartUploads`, shared by the
   server's connections), **and each first aborts every upload untouched for more than 24 hours**
   on the injected `TimeProvider`. Why: an upload session is not safe for concurrent use, and
   expiring on the next request needs no timer, so a test that advances a fake `TimeProvider` sees
   the expiry deterministically (decision 7's 24 hours).
6. **An `uploadId` created for another bucket or key is `404 NoSuchUpload`,** the error body
   carrying `UploadId`. Why: S3 binds an upload to its bucket and key, and the same answer as an
   unknown ID reveals nothing about other keys' uploads.
7. **A refused completion leaves the upload open.** Why: the client can correct its part list and
   try again, as it can against S3; only abort, expiry or a successful completion ends an upload.
8. **`Checksum*` elements inside a `Part` are skipped;** any other unexpected element is `400
   MalformedXML`. Why: clients that send checksums (decision 4.5) list them per part, and the part's
   ETag already names the bytes to assemble; skipping them keeps such a client working without
   checking a second digest the parts were not stored with.
9. **CompleteMultipartUpload checks in this order:** the body (at most 1 MiB, the signed hash,
   `Content-MD5`), `NoSuchUpload`, `MalformedXML`, `InvalidPartOrder` (not strictly ascending, so a
   repeated number too), `InvalidPart` (not uploaded, or another ETag; quotes optional, hex of
   either case), `EntityTooSmall` (a part but the last below 5 MiB). Why: a body that is not the
   one signed is refused before anything in it is trusted, an unknown upload before its document is
   read, and then from the document's shape to the parts it names, so each refusal names the first
   thing wrong.
10. **`Location` in `CompleteMultipartUploadResult` is `http(s)://<Host or listen host><path>`:**
    `https` when the connection is TLS, the request's `Host` (else the listen host), then the path as
    sent. Why: it is the URL the client just used to reach the object, so it resolves for path-style
    and virtual-hosted requests alike.
11. **Every object query other than the four multipart ones is `501 NotImplemented`,** ListParts
    and UploadPartCopy included (decision 1's one answer for an operation not served). Why: none is
    in decision 1's table, and a client that needs one should learn plainly that it is missing rather
    than receive a guess.

## Amendment 8 - `EntityTooLarge` and `IncompleteBody` close without the drain, as BL-729 built it (BL-933, 2026-11-11)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in
BL-729, and recorded here by BL-933 (BL-729 could not edit this folder while BL-701 held it). It
builds decision 7's "then close (the body is not drained)" and replaces the drain Amendment 2,
point 4 described.

1. **`HttpAnswer.DrainsBeforeClose`** (in `Surl.HttpServing`; an optional positional parameter,
   default `true`) says whether an answer that closes the connection is followed by ADR-0024's
   lingering drain. `Http1AnswerWriter.DrainBeforeCloseAsync` honours it after a refusal and after
   any other answer that closes. Why a flag rather than a new answer kind: every existing server
   keeps its bytes and its drain by default, and the writer stays the only place that decides how
   a connection ends.
2. **`S3Error.ClosesWithoutDrain` marks `EntityTooLarge` and `IncompleteBody`;** `S3Reply.Error`
   turns either into an answer with `IsRefusal: true` and `DrainsBeforeClose: false`. Wherever
   PutObject, UploadPart or CompleteMultipartUpload answer them, they carry the XML `Error` body
   (BL-720 made `Http1AnswerWriter.WriteRefusalAsync` write a refusal's `HttpAnswerBody.Bytes`
   body), `Connection: close`, within the one-second refusal write deadline, and leave the rest of
   the request body unread. Why: decision 7 says the body is not drained - reading up to
   `--max-filesize`'s worth of a body already refused only spends the server's time - and it also
   closes an UploadPart whose staged bytes ran past the limit after its body was read whole, which
   until BL-729 kept the connection open.
3. **The close is left to the transport.** On TCP, ADR-0021's lingering close for every connection
   still runs after the exchange, so the `400` is not lost to a reset while the client is still
   sending. Why that is enough: ADR-0021 already protects the answer at the transport, so the HTTP
   layer's drain only duplicated it for these two answers. The stated cost: on a transport without
   ADR-0021's lingering close, a client still sending may see a reset instead of the `400`.
4. **Tests:** `S3PutObjectTests.ServeAsync_KeepAlivePutObjectAnnouncedPastMaxFilesize_Is400EntityTooLargeClosedWithTheBodyUnread`,
   `S3PutObjectTests.ServeAsync_KeepAlivePutObjectWhoseBodyEndsEarly_Is400IncompleteBodyAndCloses`,
   and `Http1AnswerWriterTests`' refusal without the drain, refusal with it, and closing
   non-refusal without it.
## Amendment 9 - presigned URLs, `aws-chunked` and checksums, as BL-711 built them (BL-962, 2026-11-11)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in
BL-711, and recorded here by BL-962 (BL-711 could not edit this folder while BL-701 held it). It
builds decision 4.5. The code is `Surl.Authentication`'s `AwsSigV4QueryString`,
`AwsSigV4AuthenticationMethod.VerifyQueryString` and `AwsSigV4ChunkSignatureChain`, and the S3
server's `S3ObjectBodyReader`, `S3AwsChunkedDecoder` and `S3ChecksumRequest`.

1. **Contract.** `IHttpChunkSignatureChain` (`Surl.Protocol.Abstractions`), with
   `VerifyNextChunk(chunkSha256, chunkSignature)`, rides on an accepted
   `HttpAuthenticationVerdict.ChunkSignatures` (and `HttpCredentialCheck.ChunkSignatures`). Why:
   the signing key is the account's secret, which only `Surl.Authentication` holds (ADR-0084
   decision 4); the server decodes the framing and asks per chunk, and never sees a key.
2. **Refusals.** `HttpAuthenticationRefusal` gains `ExpiryOutOfRange` and `ConflictingCredentials`;
   an expired presigned URL reuses `SignatureExpired` (Azure's), whose doc now says both. The S3
   server maps them to `403 AccessDenied` `Request has expired`, `400 InvalidArgument` `X-Amz-Expires
   must be a number of seconds from 1 to 604800`, and `400 InvalidArgument` with S3's "Only one auth
   mechanism allowed…" text (decision 4.3's table). Decision 4.4's planned `Expired` is not added:
   `SignatureExpired` already says it.
3. **The presigned check** (`AwsSigV4QueryString`, `AwsSigV4AuthenticationMethod.VerifyQueryString`):
   a query with `X-Amz-Algorithm` on any service but `storage` is a presigned URL, checked even
   under `--allow-anonymous`. Order: parameters read; algorithm the scope provider's
   (`AWS4-HMAC-SHA256`); scope service the request's; `host` signed; date read and the scope's
   (`MalformedCredentials`, answered `AuthorizationHeaderMalformed`); expiry 1 to 604800; now past
   date + expiry (`SignatureExpired`) or more than 15 minutes before the date (`ClockSkew`, the
   header form's window as leeway for a fast client clock); then the signature over
   `UNSIGNED-PAYLOAD` and the canonical query without `X-Amz-Signature`. With an `Authorization`
   too it is refused unchecked and undelayed, as another scheme is. No body is bound. Why: the
   "Using Query Parameters" page's rules; time before signature, as in the header form.
4. **The S3 target sets presigned parameters aside** (`S3RequestTarget`: the seven `X-Amz-*` names,
   the security token included), so a presigned GetObject or PutObject is recognised as the
   operation it would be without the query.
5. **Streaming hashes are not bound to the body** on an `s3` request naming its service; only
   `STREAMING-AWS4-HMAC-SHA256-PAYLOAD` gets a chain. Plain HTTP and Cloud Storage keep binding the
   value as a hash, so their behaviour does not change. The server refuses every `STREAMING-` form
   it does not serve (`STREAMING-AWS4-ECDSA-P256-SHA256-PAYLOAD`, the signed `-TRAILER` forms) `501
   NotImplemented` before reading. Signed chunks with no chain (a presigned or anonymous request
   claiming them) are `403 SignatureDoesNotMatch`.
6. **`aws-chunked` framing** (`S3AwsChunkedDecoder`): accepted under `Content-Length` or HTTP
   chunked; each chunk header `<hex>[;chunk-signature=<64 hex>]`, the signature required exactly
   when signed; lines at most 4096 bytes, CRLF only; data may not pass
   `x-amz-decoded-content-length`; after the final chunk, `name:value` trailer lines (none when
   signed) up to the blank line. Malformed, cut short, or data not the decoded length is `400
   IncompleteBody` (Amendment 2, point 4: S3's code for a body that is not what the head announced);
   no decoded length is `411 MissingContentLength` (decision 4.5). `--max-filesize` is judged on the
   decoded length.
7. **`aws-chunked` is left out of the stored `Content-Encoding`,** which describes the object, not
   the upload.
8. **Checksums** (`S3ChecksumRequest`, `S3Checksum`): one of `x-amz-checksum-crc32`, `-crc32c`,
   `-sha1`, `-sha256`, or the one `x-amz-trailer` names on the unsigned-trailer form. `crc64nvme`
   is `501 NotImplemented`; two named are `400 InvalidRequest` (S3's "Expecting a single
   x-amz-checksum- header…"); a value that is not the digest's base64, a trailer on another form or
   naming no checksum, or a trailer that does not carry it, is `400 InvalidRequest` `Value for
   x-amz-checksum header is invalid.` The checksum is checked after the signed hash and
   `Content-MD5`; a mismatch is `400 BadDigest` `The <ALG> you specified did not match the
   calculated checksum.` with nothing committed; the computed value is echoed after `ETag` on
   PutObject and UploadPart. CompleteMultipartUpload's checksum fields describe the whole object
   (`<base64>-<n>`) and are not checked; an `aws-chunked` completion is `501 NotImplemented`.
9. **CRC-32C uses the BCL's `BitOperations.Crc32C`** (as the Kafka and Cloud Storage servers already
   do), so nothing is moved between libraries; only CRC-32 (ISO-HDLC) is written by hand, in
   `S3Crc32`. This corrects decision 4.5's "CRC-32 and CRC-32C are written by hand". Why: a shared
   CRC library would touch Kafka and Cloud Storage for a four-line loop the BCL already gives.
10. **A chunked body with a decoded length but no `STREAMING-` hash is `400 InvalidRequest`** (it
    was `501` while `aws-chunked` was unserved, Amendment 2, point 5): it is not `aws-chunked`, and
    a plain chunked body has no object length.

Error message texts the S3 error-code table does not give (the expiry range, the checksum
messages) are written from AWS's documented responses where known and otherwise worded plainly; no
S3 client parses them.
