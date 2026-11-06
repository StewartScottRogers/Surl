# Google Cloud Storage requirement matrix

These are the server-side requirements that `Surl.Extended.Protocol.GoogleCloudStorage` answers for
the `gs` scheme (ADR-0083 decision 9). They are written from the Cloud Storage XML API pages and the
"V4 signing process" pages that
[ADR-0110](../../Planning/Decisions/ADR-0110-how-the-google-cloud-storage-server-answers-gs.md)
decides by, never from the Curl port's client matrix. Each row binds the server. "Covered by" names
the task that proves the row until that task replaces its ID with the named test, or it says why the
row needs no test.

The pages state few requirements in RFC keywords, so each response element a server writes, each
rule of a page and each error a server owes is a row. IDs are:

- `GCS:XmlApi-<Page>-<n>` for an XML API page;
- `GCS:V4Signing-<Part>-<n>` for the signing pages;
- `GCS:Surl-<Decision>-<n>` for a rule ADR-0110 adds where the pages leave the server a choice.

## Errors ("HTTP status and error codes")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCS:XmlApi-Errors-1 | MUST | A failure has the status the page gives its code and an `application/xml` body `<Error><Code>…</Code><Message>…</Message></Error>` with the page's single-quoted XML declaration. | `GoogleCloudStorageProtocolServerTests.ServeAsync_MissingObject_Is404NoSuchKey` |
| GCS:XmlApi-Errors-2 | MUST | Each code is sent with ADR-0110 decision 1.3's status, including `NotImplemented` as `400`. | `GcsRequestAnswererTests.ServeAsync_UnlistedRequest_Is400NotImplemented`, `GoogleCloudStorageProtocolServerTests.WriteRefusalAsync_ConnectionPastALimit_Is503SlowDownThenClose` |
| GCS:XmlApi-Errors-3 | MUST | A `HEAD` error has its status and no body. | `GoogleCloudStorageProtocolServerTests.ServeAsync_HeadOfAMissingObject_HasTheStatusAndNoBody` |
| GCS:Surl-Errors-1 | MUST | `Code` and `Message` are fixed text, and no client byte is echoed (no `Details`, `Resource` or `StringToSign`). | `GoogleCloudStorageProtocolServerTests.ServeAsync_MissingObject_Is404NoSuchKey` (the whole answer pinned) |
| GCS:Surl-Operations-1 | MUST | Any request ADR-0110 decision 1 does not list is `400 NotImplemented`, judged after authentication. | `GcsRequestAnswererTests.ServeAsync_UnlistedRequest_Is400NotImplemented`, `GcsLoginJudgeTests.ServeAsync_NoTokenForAnUnlistedRequest_IsRefusedBeforeTheOperationIsJudged` |
| GCS:Surl-Operations-2 | MUST | Bucket creation and deletion, copy, `POST` uploads, subresources and `x-goog-encryption-*` are `400 NotImplemented`. | `GcsRequestAnswererTests.ServeAsync_UnlistedRequest_Is400NotImplemented` |
| GCS:XmlApi-Headers-ProjectId-1 | MAY | `x-goog-project-id` names the project. | n/a: accepted and ignored; Surl has one project (ADR-0110 decision 1) |

## Addressing and the store ("XML API overview"; ADR-0100 decision 2)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCS:XmlApi-Endpoints-1 | MUST | A path-style request `/<bucket>/<object>` names the bucket as the first segment; `/` is the service. | `GoogleCloudStorageProtocolServerTests.ServeAsync_GetObject_ReturnsTheBytesWithXGoogHashComputedAsTheApiSays`, `GcsListBucketsTests.ServeAsync_ListBuckets_ListsTheReadableTopLevelFoldersInNameOrder` |
| GCS:XmlApi-Endpoints-2 | MUST | `Host: <bucket>.<domain>` names the bucket, and the whole path is the object name. | `GoogleCloudStorageProtocolServerTests.ServeAsync_VirtualHostedRequest_ReadsTheHostsBucket`, `GcsListObjectsTests.ServeAsync_VirtualHostedListing_ListsTheHostsBucket` |
| GCS:Surl-Addressing-1 | MUST | The domains are the listen URL's DNS host, `localhost` and `storage.googleapis.com`; any other `Host`, an IP literal included, is path style. | `GcsVirtualHostTests.BucketOf_VirtualHostedName_IsTheBucket`, `GcsVirtualHostTests.BucketOf_OtherHost_IsPathStyle` |
| GCS:XmlApi-ObjectNames-1 | MUST | The object name is percent-decoded once from the path, as UTF-8. | `GoogleCloudStorageProtocolServerTests.ServeAsync_FileWrittenWithNoMetadata_IsReadWithTheIndexDefaults` |
| GCS:XmlApi-ObjectNames-2 | MUST | A name over 1024 UTF-8 bytes is `400 KeyTooLong`. | `GoogleCloudStorageProtocolServerTests.ServeAsync_NameOver1024Bytes_Is400KeyTooLong` |
| GCS:Surl-Store-1 | MUST | A bucket is a top-level folder of the shared content store; a top-level file and `.surl` are not buckets. | `GoogleCloudStorageProtocolServerTests.ServeAsync_MissingBucket_Is404NoSuchBucket` |
| GCS:Surl-Store-2 | MUST | A name the store cannot hold is `400 InvalidObjectName` on a write and `404 NoSuchKey` on a read; one conflicting with an entry of the other kind is `409 InvalidObjectName`. | `GcsPutObjectTests.ServeAsync_PutOfANameTheStoreCannotHold_Is400InvalidObjectName`, `GoogleCloudStorageProtocolServerTests.ServeAsync_NameThatIsNoFile_Is404NoSuchKey`, `GcsPutObjectTests.ServeAsync_PutConflictingWithAnEntryOfTheOtherKind_Is409InvalidObjectName` |
| GCS:Surl-Store-3 | MUST | A file another protocol wrote is served with the metadata index's defaults: `Content-Type` from the extension, the MD5 ETag and the last-write-time generation. | `GoogleCloudStorageProtocolServerTests.ServeAsync_FileWrittenWithNoMetadata_IsReadWithTheIndexDefaults`, `GoogleCloudStorageProtocolServerTests.ServeAsync_FileInAShare_IsReadWithComputedDefaults` |
| GCS:Surl-Store-4 | MUST | A bucket or object the caller may not read is `404 NoSuchBucket` or `NoSuchKey`; a write it may not make is `403 AccessDenied`. | `GoogleCloudStorageProtocolServerTests.ServeAsync_ObjectInAShareTheCallerMayNotRead_Is404NoSuchBucket`, `GcsPutObjectTests.ServeAsync_PutRefusedByShareAccess_Is403AccessDenied` |

## GET Object and HEAD Object ("GET Object", "HEAD Object", "HTTP headers")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCS:XmlApi-GetObject-1 | MUST | `GET /<bucket>/<object>` answers `200` with the object's bytes. | `GoogleCloudStorageProtocolServerTests.ServeAsync_GetObject_ReturnsTheBytesWithXGoogHashComputedAsTheApiSays` |
| GCS:XmlApi-GetObject-2 | MUST | The answer carries `Content-Length`, `Content-Type`, `ETag`, `Last-Modified`, each stored header and `x-goog-meta-*`. | `GoogleCloudStorageProtocolServerTests.ServeAsync_GetObjectWithStoredMetadata_SendsEveryStoredFieldAndMetadata` |
| GCS:XmlApi-Headers-Hash-1 | MUST | `x-goog-hash` is `crc32c=<base64 big-endian CRC-32C>,md5=<base64 MD5>` of the whole object, on a `206` too. | `GoogleCloudStorageProtocolServerTests.ServeAsync_GetObject_ReturnsTheBytesWithXGoogHashComputedAsTheApiSays`, `GoogleCloudStorageProtocolServerTests.ServeAsync_OneRange_Is206WithTheWholeObjectsHash`, `GcsObjectHashesTests` |
| GCS:XmlApi-Headers-Generation-1 | MUST | `x-goog-generation` is a positive number that changes whenever the data is replaced; `x-goog-metageneration` is `1`. | `GcsPutObjectTests.ServeAsync_PutOverAnObject_ReplacesItAndItsGeneration` |
| GCS:XmlApi-Headers-Stored-1 | MUST | `x-goog-stored-content-length` is the length, `x-goog-stored-content-encoding` the stored encoding or `identity`, and `x-goog-storage-class` is `STANDARD`. | `GoogleCloudStorageProtocolServerTests.ServeAsync_GetObject_ReturnsTheBytesWithXGoogHashComputedAsTheApiSays`, `GoogleCloudStorageProtocolServerTests.ServeAsync_GetObjectWithStoredMetadata_SendsEveryStoredFieldAndMetadata` |
| GCS:XmlApi-GetObject-3 | MUST | One `Range: bytes=` range is `206` with `Content-Range`; an unsatisfiable one is `416 InvalidRange`; several ranges are served whole with `200`. | `GoogleCloudStorageProtocolServerTests.ServeAsync_OneRange_Is206WithTheWholeObjectsHash`, `GoogleCloudStorageProtocolServerTests.ServeAsync_UnsatisfiableRange_Is416InvalidRange`, `GcsRangeSelectionTests` |
| GCS:XmlApi-GetObject-4 | MUST | A failing `If-Match` or `If-Unmodified-Since` is `412 PreconditionFailed`; a matching `If-None-Match` or an unmodified `If-Modified-Since` is `304`. | `GoogleCloudStorageProtocolServerTests.ServeAsync_FailingIfMatch_Is412PreconditionFailed`, `GoogleCloudStorageProtocolServerTests.ServeAsync_MatchingIfNoneMatch_Is304`, `GcsConditionsTests` |
| GCS:XmlApi-GetObject-5 | MUST | A failing `x-goog-if-generation-match` or `x-goog-if-metageneration-match` is `412 PreconditionFailed`; a non-numeric one is `400 InvalidArgument`. | `GoogleCloudStorageProtocolServerTests.ServeAsync_GenerationCondition_DecidesAsTheHeadersPageSays` |
| GCS:XmlApi-GetObject-6 | MUST | `generation` equal to the current one is served; any other is `404 NoSuchKey`. | `GoogleCloudStorageProtocolServerTests.ServeAsync_GenerationQuery_ServesOnlyTheCurrentGeneration` |
| GCS:XmlApi-GetObject-7 | MUST | A missing object is `404 NoSuchKey`; a missing bucket `404 NoSuchBucket`. | `GoogleCloudStorageProtocolServerTests.ServeAsync_MissingObject_Is404NoSuchKey`, `GoogleCloudStorageProtocolServerTests.ServeAsync_MissingBucket_Is404NoSuchBucket` |
| GCS:XmlApi-HeadObject-1 | MUST | `HEAD` answers GET Object's head with no body. | `GoogleCloudStorageProtocolServerTests.ServeAsync_HeadObject_ReturnsTheHeadersOnly` |

## PUT Object and DELETE Object ("PUT Object", "DELETE Object", "HTTP headers")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCS:XmlApi-PutObject-1 | MUST | `PUT /<bucket>/<object>` stores the body, replacing any object, and answers `200` with `ETag`, `x-goog-generation` and `x-goog-hash`. | `GcsPutObjectTests.ServeAsync_PutUnderAllowUploads_StoresTheObjectAndAnswersItsDigests`, `GcsPutObjectTests.ServeAsync_PutOverAnObject_ReplacesItAndItsGeneration` |
| GCS:XmlApi-PutObject-2 | MUST | `Content-Type`, `Content-Encoding`, `Content-Disposition`, `Cache-Control`, `Content-Language` and `x-goog-meta-*` are stored and returned by GET Object and HEAD Object. | `GcsPutObjectTests.ServeAsync_PutWithMetadata_IsReturnedByALaterRead` |
| GCS:XmlApi-PutObject-3 | MUST | A `PUT` without `Content-Length` is `411 MissingContentLength`; a body shorter than it is `400 IncompleteBody`, and nothing is stored. | `GcsPutObjectTests.ServeAsync_PutWithoutContentLength_Is411MissingContentLength`, `GcsPutObjectTests.ServeAsync_PutWhoseBodyEndsEarly_Is400IncompleteBodyAndStoresNothing` |
| GCS:XmlApi-Headers-ContentMd5-1 | MUST | `Content-MD5` not matching the body is `400 BadDigest`; one not the base64 of 16 bytes is `400 InvalidDigest`. | `GcsPutObjectTests.ServeAsync_PutWithAMismatchedDigest_Is400BadDigestAndKeepsTheOldObject`, `GcsPutObjectTests.ServeAsync_PutWithAMalformedContentMd5_Is400InvalidDigest` |
| GCS:XmlApi-Headers-Hash-2 | MUST | An `x-goog-hash` `md5` or `crc32c` not matching the body is `400 BadDigest`; a malformed one is `400 InvalidHash`. | `GcsPutObjectTests.ServeAsync_PutWithAMismatchedDigest_Is400BadDigestAndKeepsTheOldObject`, `GcsPutObjectTests.ServeAsync_PutWithAMalformedXGoogHash_Is400InvalidHash` |
| GCS:XmlApi-Headers-Meta-1 | MUST | Custom metadata past 8 KiB is `400 MetadataTooLarge`. | `GcsPutObjectTests.ServeAsync_PutWithMetadataPast8KiB_Is400MetadataTooLarge` |
| GCS:XmlApi-Headers-Generation-2 | MUST | `x-goog-if-generation-match: 0` stores only when no object exists, else `412 PreconditionFailed`. | `GcsPutObjectTests.ServeAsync_PutWithGenerationMatchZeroAndNoObject_IsStored`, `GcsPutObjectTests.ServeAsync_PutWithAFailingGenerationCondition_IsRefusedAndKeepsTheObject` |
| GCS:Surl-Put-1 | MUST | A zero-byte `PUT` of a name ending in `/` creates the folder; a non-empty one is refused. | `GcsPutObjectTests.ServeAsync_ZeroBytePutOfANameEndingInSlash_CreatesTheFolder`, `GcsPutObjectTests.ServeAsync_NonEmptyPutOfANameEndingInSlash_Is400InvalidObjectName`; listing the folder as `x/` is BL-775 |
| GCS:XmlApi-Headers-StorageClass-1 | MAY | `x-goog-storage-class` on a `PUT` sets the class. | n/a: accepted, and every read reports `STANDARD`; the store has one class (ADR-0110 decision 1.2) |
| GCS:XmlApi-Headers-Acl-1 | MAY | `x-goog-acl` and `x-goog-grant-*` set object ACLs. | n/a: accepted and ignored; access is ADR-0102's share access (ADR-0110 decision 1.2) |
| GCS:XmlApi-DeleteObject-1 | MUST | `DELETE /<bucket>/<object>` removes the object and answers `204`; a missing one is `404 NoSuchKey`. | `GcsRequestAnswererTests.ServeAsync_DeleteObject_Is204AndRemovesTheFile`, `GcsRequestAnswererTests.ServeAsync_DeleteOfNoObject_Is404NoSuchKey` |
| GCS:XmlApi-Uploads-1 | MAY | XML multipart and resumable uploads. | n/a: `400 NotImplemented` until BL-718 (ADR-0110 decision 1) |

## List Objects and List Buckets ("GET Bucket (List Objects)", "GET Service")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCS:XmlApi-ListObjects-1 | MUST | `GET /<bucket>` answers `ListBucketResult` in the `http://doc.s3.amazonaws.com/2006-03-01` namespace, its elements in ADR-0110 decision 6's order. | `GcsListObjectsTests.ServeAsync_ListObjects_ListsEveryObjectInNameOrder` |
| GCS:XmlApi-ListObjects-2 | MUST | Each `Contents` holds `Key`, `Generation`, `MetaGeneration`, `LastModified`, `ETag` and `Size`. | `GcsListObjectsTests.ServeAsync_ListObjects_ListsEveryObjectInNameOrder` |
| GCS:XmlApi-ListObjects-3 | MUST | Names are listed in ascending UTF-8 byte order. | `GcsListObjectsTests.ServeAsync_ListObjects_ListsEveryObjectInNameOrder`, `GcsNameOrderTests` |
| GCS:XmlApi-ListObjects-4 | MUST | `prefix` limits the names; with `delimiter`, names sharing a prefix up to the first delimiter after `prefix` roll up into one `CommonPrefixes`. | `GcsListObjectsTests.ServeAsync_DelimiterSlash_RollsFoldersUpIntoCommonPrefixes`, `GcsListObjectsTests.ServeAsync_Prefix_ListsOnlyTheNamesUnderIt` |
| GCS:XmlApi-ListObjects-5 | MUST | `max-keys` defaults to 1000, above 1000 is served as 1000, and bounds `Contents` and `CommonPrefixes` together. | `GcsListObjectsTests.ServeAsync_MaxKeysAbove1000_IsServedAs1000`, `GcsListObjectsTests.ServeAsync_Version2PagedByContinuationToken_ResumesAfterTheLastEntry` |
| GCS:XmlApi-ListObjects-6 | MUST | Version 1: `marker` begins after that name; a truncated page has `IsTruncated` true and `NextMarker`. | `GcsListObjectsTests.ServeAsync_Version1PagedByMaxKeysAndMarker_ResumesAfterTheMarker` |
| GCS:XmlApi-ListObjects-7 | MUST | Version 2 (`list-type=2`): a truncated page has `NextContinuationToken`, which resumes after the last entry; `start-after` is ignored when `continuation-token` is present; `KeyCount` counts the entries. | `GcsListObjectsTests.ServeAsync_Version2PagedByContinuationToken_ResumesAfterTheLastEntry` |
| GCS:XmlApi-ListObjects-8 | MUST | `encoding-type=url` percent-encodes names and prefixes; another value is `400 InvalidArgument`. | `GcsListObjectsTests.ServeAsync_EncodingTypeUrl_PercentEncodesNames`, `GcsListObjectsTests.ServeAsync_BadListingParameter_Is400InvalidArgument` |
| GCS:XmlApi-ListObjects-9 | MUST | `fetch-owner=true` with `list-type=2` adds `Owner` to each `Contents`. | `GcsListObjectsTests.ServeAsync_Version2PagedByContinuationToken_ResumesAfterTheLastEntry` |
| GCS:XmlApi-ListObjects-10 | MUST | A parameter given with the wrong `list-type`, a bad `max-keys`, or a token that does not decode is `400 InvalidArgument`. | `GcsListObjectsTests.ServeAsync_BadListingParameter_Is400InvalidArgument` |
| GCS:XmlApi-ListObjects-11 | MAY | `versions`, `key-marker`, `version-id-marker` and `generation-marker` list object versions. | n/a: `400 NotImplemented`; the store keeps one version a file (ADR-0110 decision 6) |
| GCS:XmlApi-ListBuckets-1 | MUST | `GET /` answers `ListAllMyBucketsResult` with `Owner` and each readable bucket's `Name` and `CreationDate`. | `GcsListBucketsTests.ServeAsync_ListBuckets_ListsTheReadableTopLevelFoldersInNameOrder` |

## Authentication ("XML API overview", RFC 6750, "V4 signing process", "Canonical requests")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCS:Surl-Auth-1 | MUST | With anonymous off, a request with no credential is `401 AuthenticationRequired` with `WWW-Authenticate: Bearer realm="storage"`. | `GcsLoginJudgeTests.ServeAsync_NoTokenWithAnonymousOff_Is401AuthenticationRequiredWithTheBearerChallenge` |
| GCS:Surl-Auth-2 | MUST | Under `--allow-anonymous`, unauthenticated reads are served and an anonymous write is `401 AuthenticationRequired`. | `GcsLoginJudgeTests.ServeAsync_NoTokenUnderAllowAnonymous_ServesTheRead`, `GcsLoginJudgeTests.ServeAsync_AnonymousWrite_Is401AuthenticationRequired` |
| GCS:Surl-Auth-3 | MUST | A write needs `--allow-uploads`; without it a logged-in write is `403 AccessDenied`. | `GcsPutObjectTests.ServeAsync_PutWithoutAllowUploads_Is403AccessDeniedAndStoresNothing`, `GcsRequestAnswererTests.ServeAsync_DeleteWithoutAllowUploads_Is403AccessDenied` |
| GCS:XmlApi-OAuth2-1 | MUST | `Authorization: Bearer <token>` is served when the token is an empty-name account's password. | `GoogleCloudStorageProtocolServerTests.ServeAsync_GetObject_ReturnsTheBytesWithXGoogHashComputedAsTheApiSays` |
| GCS:XmlApi-OAuth2-2 | MUST | A Bearer token that is no account is `401 InvalidAuthentication` with an `error="invalid_token"` challenge. | `GcsLoginJudgeTests.ServeAsync_TokenThatIsNoAccount_Is401InvalidAuthenticationWithTheInvalidTokenChallenge` |
| GCS:Surl-Auth-4 | MUST | Another `Authorization` scheme (Basic, Digest, `AWS4-HMAC-SHA256`, `GOOG1`, `AWS`) is `400 InvalidAuthentication`. | `GcsLoginJudgeTests.ServeAsync_AnotherScheme_Is400InvalidAuthentication` |
| GCS:Surl-Auth-5 | MUST | The checked account (or anonymous) is what the store's share access check receives. | `GcsPutObjectTests.ServeAsync_PutRefusedByShareAccess_Is403AccessDenied`, `GoogleCloudStorageProtocolServerTests.ServeAsync_ObjectInAShareTheCallerMayNotRead_Is404NoSuchBucket` |
| GCS:V4Signing-Authorization-1 | MUST | `GOOG4-HMAC-SHA256 Credential=<id>/<date>/<location>/storage/goog4_request, SignedHeaders=…, Signature=…` is checked with the HMAC key from the account set; a signature worked by hand from the page's steps verifies at its date. | BL-430 |
| GCS:V4Signing-Authorization-2 | MUST | A header that does not parse, a scope not ending `/storage/goog4_request`, or `host` not signed is `400 InvalidArgument`. | `GcsLoginJudgeTests.ServeAsync_SignedRequestRefused_IsTheErrorDecision34Gives` (the reason, `MalformedCredentials`, is `GoogleV4HmacAuthenticationMethodTests`' (BL-430)) |
| GCS:V4Signing-CanonicalRequest-1 | MUST | The canonical request is built as the SigV4 one is, with the URI encoded once, the query re-encoded and sorted, and the signed headers' values trimmed; the AWS SigV4 check's bytes do not change. | BL-430 |
| GCS:V4Signing-SigningKey-1 | MUST | The signing key is chained from `GOOG4<secret>` over the date, the location, `storage` and `goog4_request`. | BL-430 |
| GCS:V4Signing-Scope-1 | MUST | Any location in the scope (`auto`, `us-east1`, …) is accepted. | BL-430 |
| GCS:V4Signing-Credential-1 | MUST | An access ID that is no account is `403 InvalidAccessKeyId`, checked against a random secret so it takes the same time. | `GcsLoginJudgeTests.ServeAsync_SignedRequestRefused_IsTheErrorDecision34Gives` (the timing is `Surl.Authentication`'s, BL-430) |
| GCS:V4Signing-Signature-1 | MUST | A signature that does not verify is `403 SignatureDoesNotMatch`. | `GcsLoginJudgeTests.ServeAsync_SignedGetWithAWrongSignature_Is403SignatureDoesNotMatch`, `GcsLoginJudgeTests.ServeAsync_SignedRequestRefused_IsTheErrorDecision34Gives` |
| GCS:V4Signing-Date-1 | MUST | `x-goog-date` (or `Date`) more than 15 minutes from the server's clock, or not the scope's date, is `403 RequestTimeTooSkewed`. | `GcsLoginJudgeTests.ServeAsync_SignedRequestRefused_IsTheErrorDecision34Gives` |
| GCS:V4Signing-Payload-1 | MUST | A signed request without `x-goog-content-sha256`, or without `x-goog-date` and `Date`, is `400 MissingSecurityHeader`. | `GcsLoginJudgeTests.ServeAsync_SignedRequestRefused_IsTheErrorDecision34Gives` |
| GCS:V4Signing-Payload-2 | MUST | A body not hashing to `x-goog-content-sha256` is `400 BadDigest` and is not stored. | `GcsPutObjectTests.ServeAsync_SignedPutWhoseBodyIsNotTheOneSigned_Is400BadDigestAndKeepsTheOldObject`, `GcsPutObjectTests.ServeAsync_SignedFolderPut_ChecksTheEmptyBodyAgainstTheSignedHash`, `GcsLoginJudgeTests.ServeAsync_SignedGetWithABody_ReadsAndChecksTheBodyBeforeAnswering` |
| GCS:V4Signing-Payload-3 | MUST | `UNSIGNED-PAYLOAD` leaves the body unbound and is served. | `GcsLoginJudgeTests.ServeAsync_SignedGetWorkedByHandInBL430_IsServedAsTheAccessIdsAccount` |
| GCS:V4Signing-QueryString-1 | MAY | Signed URLs (`X-Goog-Signature` in the query). | n/a: ADR-0110 decision 3.3 serves the `Authorization` header form only; a query signature is decision 1's `400 NotImplemented` |
| GCS:V4Signing-Rsa-1 | MAY | `GOOG4-RSA-SHA256` service-account signatures. | n/a: the account set holds HMAC keys, not service-account keys; the scheme is decision 3.4's `400 InvalidAuthentication` |

## TLS and limits (ADR-0110 decisions 5 and 7; ADR-0006)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCS:Surl-Tls-1 | MUST | `gs` is implicit TLS, with `--cert` or `--self-signed` required (exit 58 without), and ALPN `http/1.1` only. | n/a: the `surl` composition's row, proved by BL-433, not by the server library |
| GCS:Surl-Tls-2 | MUST | `--gs-plaintext` serves plain HTTP/1.1 and is warned on start. | n/a: the `surl` composition's row, proved by BL-433, not by the server library |
| GCS:Surl-Tls-3 | MUST | A Bearer token over plain text without `--allow-plaintext-auth` is `401 InvalidAuthentication`; a V4 HMAC signature is served. | `GcsLoginJudgeTests.ServeAsync_TokenThatIsNoAccount_Is401InvalidAuthenticationWithTheInvalidTokenChallenge` (every token the session refuses); the plain-text refusal itself is the `Surl.Authentication` session, wired by BL-433; V4 HMAC over plain text is served: `GcsLoginJudgeTests.ServeAsync_SignedGetWorkedByHandInBL430_IsServedAsTheAccessIdsAccount` (a connection with no TLS session) |
| GCS:Surl-Limits-1 | MUST | A head past `--max-request-head` is `400 MaxMessageLengthExceeded`, then close. | BL-774; until then `GoogleCloudStorageProtocolServerTests.ServeAsync_HeadPastMaxRequestHead_IsRefused431AndClosedBySurlHttpServing` pins the `431` of `Surl.HttpServing` |
| GCS:Surl-Limits-2 | MUST | A body past `--max-filesize` is `400 EntityTooLarge`, then close. | `GcsPutObjectTests.ServeAsync_PutAnnouncedPastMaxFilesize_Is400EntityTooLarge`, `GcsHashingStreamTests.WriteAsync_PastTheUploadLimit_HashesTheRestAndDropsIt` |
| GCS:Surl-Limits-3 | MUST | A connection past `--max-connections` or `--max-connections-per-address` is `503 SlowDown`, then close. | `GoogleCloudStorageProtocolServerTests.WriteRefusalAsync_ConnectionPastALimit_Is503SlowDownThenClose` |
| GCS:Surl-Limits-4 | MUST | `--head-timeout`, `--idle-timeout` and `--max-time` close with no bytes. | n/a: the timeouts of `Surl.HttpServing`, which the server takes unchanged through `Http1ConnectionServer`, proved in `Surl.HttpServing.UnitTests` |
| GCS:Surl-Limits-5 | MUST | A full metadata index is `403 InsufficientQuota`. | `GcsPutObjectTests.ServeAsync_PutWhenTheIndexIsFull_Is403InsufficientQuotaAndRemovesTheObject` |
