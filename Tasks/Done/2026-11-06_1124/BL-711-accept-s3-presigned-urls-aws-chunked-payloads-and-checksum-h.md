---
id: BL-711
title: Accept S3 presigned URLs, aws-chunked payloads and checksum headers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-420]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Documentation/Conformance/Extended/S3.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-711 — Accept S3 presigned URLs, aws-chunked payloads and checksum headers

## Goal

The S3 server serves SigV4 presigned query strings, `aws-chunked` bodies (`STREAMING-AWS4-HMAC-SHA256-PAYLOAD`
and `STREAMING-UNSIGNED-PAYLOAD-TRAILER`) and checks `x-amz-checksum-crc32`, `-crc32c`, `-sha1` and `-sha256`.

## Context

- Decision: ADR-0108 decision 4.5; matrix rows `S3:SigV4-QueryString-1` and `-2`, `S3:SigV4-Chunked-1` to `-3`,
  `S3:PutObject-6` and `-7`.
- Specification: "Authenticating Requests: Using Query Parameters (AWS Signature Version 4)", "Signature
  Calculations for the Authorization Header: Transferring Payload in Multiple Chunks (Chunked Upload)" (its worked
  example is a test vector) and the User Guide's "Checking object integrity".
- The query-string check goes beside the header check in `Surl.Authentication`'s SigV4 method, reached through
  `IHttpAuthenticationSession` (ADR-0084 decision 4); the server never references `Surl.Authentication`.
- CRC-32 and CRC-32C are hand-written where the BCL has none (`System.IO.Hashing` is a package); check whether
  `Surl.Compression` or the Kafka server already has a CRC-32C to move rather than copy (ADR-0105).

## Acceptance criteria

- [x] A named test accepts the "Using Query Parameters" page's worked example at its date, and refuses it expired
      with `403 AccessDenied`.
- [x] A named test accepts the chunked-upload page's worked example, and a tampered chunk is `403
      SignatureDoesNotMatch`.
- [x] Named tests show each checksum header checked (`400 BadDigest`) and echoed, and `crc64nvme` `501
      NotImplemented`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every library touched keeps 100% line
      and branch coverage.

## Notes

Filed by BL-417 (ADR-0108): AWS's SDKs and CLI send these forms.

### How each criterion is met

- Presigned: `AwsSigV4PresignedAndChunkedTests.PresignedWorkedExample_AtItsDate_IsAcceptedAsTheKeysAccount`
  (the page's URL and signature `aeeed9bb…`), `…_ASecondPastItsExpiry_IsForbiddenAsExpired` (`SignatureExpired`),
  mapped to `403 AccessDenied` `Request has expired` by `S3LoginTests.ErrorOf_PresignedUrlRefusals_HaveTheirErrors`.
- Chunked: `AwsSigV4PresignedAndChunkedTests.ChunkedWorkedExample_SeedSignature_IsAcceptedWithAChainThatVerifiesEachChunk`
  reproduces the seed (`4f232c43…`) and the three chunk signatures; `…_ATamperedChunk_FailsAndLeavesTheChainWhereItWas`.
  In S3, `S3ObjectBodyReaderTests.ServeAsync_ChunkedUploadWorkedExample_StoresTheDecodedObjectAndChecksEveryChunk`
  replays the example's 66824 bytes and stores 66560, and
  `…_ChunkedUploadWithATamperedChunk_Is403SignatureDoesNotMatchAndNotStored`.
- Checksums: `S3ObjectBodyReaderTests.ServeAsync_PutObjectWithItsChecksumField_IsStoredAndTheChecksumEchoed` and
  `…_PutObjectWithAnotherBodysChecksum_Is400BadDigestNamingTheAlgorithm` (one row each for crc32, crc32c, sha1,
  sha256), `…_PutObjectWithCrc64Nvme_Is501NotImplementedAndNotStored`; check values in `S3ChecksumTests`.
- Gates: `dotnet build -warnaserror` clean; fast tests green (79 test projects); `Measure-CodeQuality.ps1` over the
  two test projects: `Surl.Extended.Protocol.S3.UnitLibrary` 100 / 100. `Surl.Authentication.UnitLibrary` reads
  99.75 / 99.54, all of it pre-existing and untouched: the source-generated regex code under `obj/` and
  `HttpAuthenticationSession.AnswerCheckAsync`'s one branch (this task changed neither it nor its tests). Every
  new Authentication member is 100 / 100. `Surl.Protocol.Abstractions` gained an enum's members, an interface and a
  record parameter, no code.

### Decisions (Decided by Claude under Stewart's delegation; to go into ADR-0108 as Amendment 4, BL-962)

BL-701 holds `Documentation/Planning/Decisions`, so ADR-0108 could not be edited here; BL-962 records these there,
as BL-727 did for BL-419.

1. **Contract.** `IHttpChunkSignatureChain` (Abstractions) with `VerifyNextChunk(chunkSha256, chunkSignature)`
   rides on an accepted `HttpAuthenticationVerdict.ChunkSignatures` (and `HttpCredentialCheck.ChunkSignatures`).
   Why: the signing key is the account's secret, which only `Surl.Authentication` holds (ADR-0084 decision 4); the
   server decodes the framing and asks per chunk, and never sees a key.
2. **Refusals.** `HttpAuthenticationRefusal` gains `ExpiryOutOfRange` and `ConflictingCredentials`; an expired
   presigned URL reuses `SignatureExpired` (Azure's), whose doc now says both. S3 maps them to `403 AccessDenied`
   `Request has expired`, `400 InvalidArgument` `X-Amz-Expires must be a number of seconds from 1 to 604800`, and
   `400 InvalidArgument` with S3's "Only one auth mechanism allowed…" text.
3. **Presigned check** (`AwsSigV4QueryString`, `AwsSigV4AuthenticationMethod.VerifyQueryString`): a query with
   `X-Amz-Algorithm` on any service but `storage` is a presigned URL, checked even under `--allow-anonymous`.
   Order: parameters read, algorithm the scope provider's (`AWS4-HMAC-SHA256`), scope service the request's,
   `host` signed, date read and the scope's (`MalformedCredentials` → `AuthorizationHeaderMalformed`); expiry 1 to
   604800; now past date + expiry (`SignatureExpired`) or more than 15 minutes before the date (`ClockSkew`, the
   header form's window as leeway for a fast client clock); then the signature over `UNSIGNED-PAYLOAD`, the
   canonical query without `X-Amz-Signature`. With an `Authorization` too it is refused unchecked and undelayed,
   as another scheme is. No body is bound. Why: the page's rules; time before signature as in the header form.
4. **The S3 target sets presigned parameters aside** (`S3RequestTarget`: the seven `X-Amz-*` names, security
   token included), so a presigned GetObject or PutObject is recognised as the operation without a query.
5. **Streaming hashes are not bound to the body** on an `s3` request naming its service; only
   `STREAMING-AWS4-HMAC-SHA256-PAYLOAD` gets a chain. Plain HTTP and Cloud Storage keep binding the value as a
   hash, so their behaviour does not change. The server refuses every `STREAMING-` form it does not serve
   (`STREAMING-AWS4-ECDSA-P256-SHA256-PAYLOAD`, the signed `-TRAILER` forms) `501 NotImplemented` before reading.
   Signed chunks with no chain (a presigned or anonymous request claiming them) are `403 SignatureDoesNotMatch`.
6. **`aws-chunked` framing** (`S3AwsChunkedDecoder`): accepted under `Content-Length` or HTTP chunked; each
   header `<hex>[;chunk-signature=<64 hex>]`, the signature required exactly when signed; lines at most 4096
   bytes, CRLF only; data may not pass `x-amz-decoded-content-length`; after the final chunk, `name:value`
   trailer lines (none when signed) up to the blank line. Malformed, cut short, or data not the decoded length is
   `400 IncompleteBody` (Amendment 2 decision 4's code for a body that is not what the head announced); no decoded
   length `411 MissingContentLength` (decision 4.5). `--max-filesize` is judged on the decoded length.
7. **`aws-chunked` is left out of the stored `Content-Encoding`**, which describes the object, not the upload.
8. **Checksums** (`S3ChecksumRequest`, `S3Checksum`): one of `x-amz-checksum-crc32`, `-crc32c`, `-sha1`,
   `-sha256`, or the one `x-amz-trailer` names on the unsigned-trailer form. `crc64nvme` `501`; two named `400
   InvalidRequest` (S3's "Expecting a single x-amz-checksum- header…"); a value that is not the digest's base64,
   a trailer on another form or naming no checksum, or a trailer that does not carry it, `400 InvalidRequest`
   `Value for x-amz-checksum header is invalid.` Checked after the signed hash and `Content-MD5`; a mismatch is
   `400 BadDigest` `The <ALG> you specified did not match the calculated checksum.`, nothing committed; the
   computed value is echoed after `ETag` on PutObject and UploadPart. CompleteMultipartUpload's checksum fields
   describe the whole object (`<base64>-<n>`) and are not checked; an `aws-chunked` completion is `501`.
9. **CRC-32C uses the BCL's `BitOperations.Crc32C`** (as Kafka and Cloud Storage already do), so nothing is moved
   between libraries; only CRC-32 (ISO-HDLC) is hand-written, in `S3Crc32`. Why: a shared CRC library would touch
   Kafka and Cloud Storage, which other lanes own, for a four-line loop the BCL already gives.
10. **A chunked body with a decoded length but no `STREAMING-` hash is now `400 InvalidRequest`** (it was `501`
    while aws-chunked was unserved, Amendment 2 decision 5): it is not aws-chunked, and a plain chunked body has no
    object length.

### Scope

- Added `Documentation/Conformance/Extended/S3.md` to `touches` to name the proving tests on its BL-711 rows;
  no task in Doing names it.
- Error message texts not given by the S3 error-code table (the expiry range, the checksum messages) are written
  from AWS's documented responses where known and otherwise worded plainly; no S3 client parses them.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. S3 serves SigV4 presigned URLs, aws-chunked bodies (signed chunks and unsigned trailer) and x-amz-checksum-crc32/crc32c/sha1/sha256; crc64nvme 501
