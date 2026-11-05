---
id: BL-431
title: Create the Google Cloud Storage server and answer object reads, writes and listings
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-429, BL-404, BL-407, BL-636, BL-656]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Surl.slnx, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-431 — Create the Google Cloud Storage server and answer object reads, writes and listings

## Goal

`Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` exists. It answers XML API GET, HEAD and PUT Object and
List Objects from the content store with bearer authentication, and refuses with the XML API's error bodies.

## Context

- Decision: BL-429's ADR, decisions 1 to 3. Rules: BL-543's ADR.
- New projects in `Protocols/FileTransfer/`, listed in `Surl.slnx`. Namespace
  `Surl.Extended.Protocol.GoogleCloudStorage`. References: `Surl.Protocol.Abstractions`, `Surl.Content`, and
  BL-407's library.
- XML API: object responses carry `x-goog-generation`, `x-goog-hash` (crc32c and md5) and `ETag`; List Objects
  returns `ListBucketResult` with `prefix`, `delimiter`, `marker` and `max-keys`; errors are
  `<Error><Code>…</Code><Message>…</Message></Error>`. CRC32C is computed with `System.Numerics.BitOperations.Crc32C`.
- V4 HMAC is wired in by BL-432. This task's tests use bearer tokens and anonymous reads.
- BL-429's ADR is ADR-0110 (2026-11-05). Beyond GET, HEAD, PUT and List Objects, its decision 1 also gives this
  task DELETE Object and List Buckets (`GET /`), both listing versions (version 1 `marker`/`NextMarker` and
  version 2 continuation tokens), `400 NotImplemented` for every other request, and its decision 3.4 gives the
  refusal codes (`401 AuthenticationRequired` with the Bearer challenge for no credential). The matrix is
  `Documentation/Conformance/Extended/GoogleCloudStorage.md`; replace each `BL-431` there with the test's name.
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 2): objects are files in the one shared
  `ContentStore`. PUT Object's content type, `x-goog-meta-*`, generation and hashes go to BL-636's object metadata,
  not a private store; a file written by another protocol is answered with BL-636's defaults. Every store call
  passes the checked account (or anonymous) and scheme `gs` to BL-656's share access check; a refusal is answered
  as BL-429's ADR names it. Bearer tokens come from the one account set.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - GET and HEAD of an object with `x-goog-hash` computed as the API says;
      - a PUT stored under `--allow-uploads` and refused without it;
      - a listing paged with `max-keys` and `marker`;
      - `delimiter=/` prefixes;
      - a missing object gets `404 NoSuchKey`;
      - no token with anonymous off is refused as the ADR says;
      - a file put into the content store with no metadata (as FTP writes it) read with BL-636's defaults;
      - a write refused by share access answered with the ADR's error;
      - DELETE Object `204`, List Buckets listing the readable top-level folders, a `list-type=2` listing paged
        by `continuation-token`, and an unlisted request (a `POST`) answered `400 NotImplemented` (ADR-0110
        decision 1).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Plan: follow the S3 server's shape (ADR-0110 says so where the APIs agree). Copied and renamed S3's range,
  condition, walk, name-order, continuation-token, query and index-key helpers; wrote GCS's own error table and
  one-line body (no echoed bytes), Bearer login judge (modelled on Azure's), `x-goog-hash` hashing
  (`BitOperations.Crc32C` + MD5 in one pass), generation conditions, PUT, DELETE, both listing versions and
  List Buckets. 197 tests in `Surl.Extended.Protocol.GoogleCloudStorage.UnitTests`; the library is at 100% line
  and 100% branch coverage (cobertura from the MSTest coverage collector).
- Where each acceptance criterion is proved: GET/HEAD and `x-goog-hash` -
  `GoogleCloudStorageProtocolServerTests.ServeAsync_GetObject_ReturnsTheBytesWithXGoogHashComputedAsTheApiSays`,
  `..._HeadObject_ReturnsTheHeadersOnly`, `GcsObjectHashesTests` (the CRC-32C check value `123456789` ->
  `0xE3069283`); PUT under and without `--allow-uploads` - `GcsPutObjectTests.ServeAsync_PutUnderAllowUploads_StoresTheObjectAndAnswersItsDigests`,
  `..._PutWithoutAllowUploads_Is403AccessDeniedAndStoresNothing`; paging - `GcsListObjectsTests.ServeAsync_Version1PagedByMaxKeysAndMarker_ResumesAfterTheMarker`;
  `delimiter=/` - `..._DelimiterSlash_RollsFoldersUpIntoCommonPrefixes`; `404 NoSuchKey` -
  `GoogleCloudStorageProtocolServerTests.ServeAsync_MissingObject_Is404NoSuchKey`; no token -
  `GcsLoginJudgeTests.ServeAsync_NoTokenWithAnonymousOff_Is401AuthenticationRequiredWithTheBearerChallenge`;
  FTP-style file with no metadata - `..._FileWrittenWithNoMetadata_IsReadWithTheIndexDefaults`; share-refused
  write - `GcsPutObjectTests.ServeAsync_PutRefusedByShareAccess_Is403AccessDenied`; DELETE `204` -
  `GcsRequestAnswererTests.ServeAsync_DeleteObject_Is204AndRemovesTheFile`; List Buckets -
  `GcsListBucketsTests.ServeAsync_ListBuckets_ListsTheReadableTopLevelFoldersInNameOrder`; `list-type=2` by
  token - `GcsListObjectsTests.ServeAsync_Version2PagedByContinuationToken_ResumesAfterTheLastEntry`; `POST` -
  `GcsRequestAnswererTests.ServeAsync_UnlistedRequest_Is400NotImplemented`.
- Touches widened: `Documentation/Conformance/Extended/GoogleCloudStorage.md`, because the task's Context says
  to replace each `BL-431` in the matrix with the test's name. No task in `Doing` names it.
- Choices with a sensible default (recorded, no new ADR needed - each follows ADR-0110 or ADR-0108's rule):
  - `GOOG4-HMAC-SHA256` and every other non-Bearer `Authorization` is `400 InvalidAuthentication` until BL-432
    wires V4 HMAC in; only Bearer reaches the authentication session.
  - The Bearer method logs in as the empty name, which is served as the anonymous `ContentCaller` (as Azure
    does) but may write; an account name is used when the session gives one.
  - Listings (List Objects and List Buckets) need `--list-directories` (off: `403 AccessDenied`), as S3's do:
    the store refuses a listing otherwise (ADR-0006).
  - DELETE needs `--allow-uploads` (ADR-0110 decision 3.1: "Writes need `--allow-uploads`"); `?generation=`
    on DELETE is `400 NotImplemented` (decision 1: "`?generation=` on a write"). A parameter the operation's
    page does not give is `NotImplemented`; `GET /` with any query too.
  - A bucket's `CreationDate` is the folder's last-write time: the store keeps no creation time.
  - A file in a share (which the index does not key) answers its last-write time as its generation, and a PUT
    into a share answers the written file's last-write time.
  - `x-goog-if-generation-match` / `-metageneration-match` are checked on DELETE too (decision 1: "on every
    object operation").
- Left for follow-up tasks (filed): BL-773 caches the CRC-32C in the object metadata index (today GET and
  HEAD read the whole file to hash it; `Surl.Content` was outside this task); BL-774 answers a head past
  `--max-request-head` `400 MaxMessageLengthExceeded` once BL-720's `Surl.HttpServing` seam exists (today it is
  `431`, pinned by a test); BL-775 lists a folder PUT over `gs` as `x/` (the index keys files only).

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-636 and BL-656; uses the shared store's metadata and access check.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The gs server answers XML API GET, HEAD, PUT and DELETE Object, List Objects (both versions) and List Buckets with Bearer or anonymous logins and the XML API error bodies
