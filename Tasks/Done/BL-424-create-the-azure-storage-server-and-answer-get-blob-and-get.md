---
id: BL-424
title: Create the Azure Storage server and answer Get Blob and Get Blob Properties
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-423, BL-404, BL-407]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-424 — Create the Azure Storage server and answer Get Blob and Get Blob Properties

## Goal

`Surl.Extended.Protocol.AzureStorage.UnitLibrary` exists. It answers Get Blob and Get Blob Properties from the
content store, checks the required `x-ms-*` headers and the bearer login, and refuses anything else with the
Blob service's XML error and `x-ms-error-code`.

## Context

- Decision: BL-423's ADR, decisions 1 to 5. Rules: BL-543's ADR.
- New projects in `Protocols/FileTransfer/`, listed in `Surl.slnx` under `/Protocols/FileTransfer/`. Namespace
  `Surl.Extended.Protocol.AzureStorage`. References: `Surl.Protocol.Abstractions`, `Surl.Content`, and
  BL-407's library.
- The bearer check reaches the accounts through `Surl.Protocol.Abstractions` contracts, never `Surl.Authentication`
  directly.
- Expected bytes come from the REST API pages' response examples: `ETag`, `Last-Modified`, `x-ms-blob-type`,
  `x-ms-request-id`, `x-ms-version`, and the error codes `BlobNotFound`, `ContainerNotFound`,
  `MissingRequiredHeader`, `AuthenticationFailed`, `InvalidAuthenticationInfo`.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - Get Blob returns the bytes and headers;
      - Get Blob Properties returns the headers only;
      - a missing blob gets `404 BlobNotFound`;
      - a missing container gets `404 ContainerNotFound`;
      - a missing `x-ms-version` is refused as the ADR says;
      - no bearer token with anonymous off gets the ADR's error;
      - a wrong token gets `AuthenticationFailed` or the ADR's code.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Built on the S3 server's shape (BL-418): `AzureStorageProtocolServer` over `Http1ConnectionServer`,
  `AzureBlobRequestAnswerer` as the handler, `AzureReply`/`AzureError`/`AzureErrorBody` for the answers. The
  bearer login goes through `IAuthenticationPolicy.StartHttpConnection(connection.TlsSession)`, so the library
  references only Abstractions, Content and HttpServing; `ProtocolIsolationTests` passes.
- Named tests (in `AzureStorageProtocolServerTests` unless noted): `ServeAsync_GetBlob_ReturnsTheBytesAndHeaders`,
  `ServeAsync_GetBlobProperties_ReturnsTheHeadersOnly`, `ServeAsync_MissingBlob_Is404BlobNotFound`,
  `ServeAsync_MissingContainer_Is404ContainerNotFound`, `ServeAsync_BearerWithoutAVersion_Is400MissingRequiredHeader`,
  `ServeAsync_NoCredentialsWithAnonymousOff_Is401NoAuthenticationInformationWithTheChallenge`,
  `ServeAsync_WrongBearerToken_Is401InvalidAuthenticationInfoWithTheChallenge`; ranges, conditions and MD5 in
  `AzureBlobAnswerTests`. 100 tests; `Measure-CodeQuality.ps1` reports 100% line, 100% branch, worst CRAP 10.
- Defaults taken where ADR-0109 leaves a detail open (recorded here, not in a new ADR, since none changes a
  decision):
  - Only `az` is in `Schemes`; BL-426 adds `abfs` and `abfss` with the Data Lake answers.
  - A `GET`/`HEAD` path naming no blob and no operation (`/`, `/<container>`) is `400 InvalidUri` ("Common REST
    API error codes": "The requested URI does not represent any resource on the server."), until BL-425 and
    BL-716 serve those paths' operations.
  - `snapshot` and `versionid` are refused `400 InvalidQueryParameterValue` beside `comp` and `restype`, since
    serving the current blob for a snapshot would be wrong; other query parameters (`timeout`) are ignored.
  - Until BL-717, `Authorization: SharedKey` is `400 InvalidAuthenticationInfo`, as any scheme the server does
    not check; a SAS query is ignored.
  - A session `Forbidden` (a bearer secret refused over plain text, ADR-0109 decision 6) is answered as a
    challenge: `401 InvalidAuthenticationInfo` with credentials, `NoAuthenticationInformation` without.
  - `Content-MD5` is the base64 of the ETag when the ETag is the index's quoted hexadecimal MD5, and left out
    otherwise (an ETag stored by another protocol's upload). `x-ms-range-get-content-md5: true` without a range
    is ignored.
  - `x-ms-client-request-id` is echoed when 1 to 1024 characters of `!` to `~`.
  - The `Reason` of `InvalidQueryParameterValue` is the fixed sentence `This operation is not served by this server.`
- Gaps filed (they need projects outside this task's `touches`): BL-725 (credentials checked under
  `--allow-anonymous` - `Surl.Authentication`'s session proceeds without running the bearer verifier there, so a
  bearer request under anonymous is served as an anonymous read today - and the path-style account segment,
  which needs account names through Abstractions); BL-726 (`400 InvalidInput` for a head past
  `--max-request-head`, after BL-720's HttpServing seam).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. az serves Get Blob and Get Blob Properties with bearer and anonymous logins and the Blob XML errors
