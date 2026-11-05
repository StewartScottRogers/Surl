---
id: BL-717
title: Check Azure Shared Key and SAS logins for the Azure Storage server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-424, BL-420]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-717 — Check Azure Shared Key and SAS logins for the Azure Storage server

## Goal

The Azure Storage server accepts `Authorization: SharedKey` and `SharedKeyLite` on the Blob and Data Lake
endpoints, and service and account shared access signatures in the query, each checked against the one account
set, and answers every failure with ADR-0109 decision 3's code.

## Context

- Decision: ADR-0109 decision 3 (3.2 Shared Key, 3.3 SAS, 3.4 the refusals). Rules: ADR-0083, ADR-0100 decision
  4.1 (name = storage account name, password = the account key's base64 text).
- Specifications: "Authorize with Shared Key" (the string to sign for the Blob and Data Lake services, the
  canonicalized headers and resource, `SharedKeyLite`), "Create a service SAS" and "Create an account SAS" (the
  string to sign per `sv`, `se`/`st`, `sp`, `spr`, `sip`).
- The methods live in `Surl.Authentication` behind `IHttpAuthenticationSession`, as BL-420's SigV4 does; the
  server reads the refusal reason BL-420 adds to `HttpAuthenticationVerdict` and never references
  `Surl.Authentication` or an account (ADR-0084 decision 4).
- Expected bytes come from the pages' worked examples, never from the Curl port (which does not sign Shared Key).

## Acceptance criteria

- [x] Named tests show a Shared Key request verified with the page's string to sign, for a Blob and a Data Lake
      request, and a `SharedKeyLite` one.
- [x] Named tests show a wrong signature, an unknown account and a date more than 15 minutes off each answered
      `403 AuthenticationFailed`, and a header that does not parse `400 InvalidAuthenticationInfo`.
- [x] Named tests show a service SAS and an account SAS served, an expired one `403 AuthenticationFailed`, a
      permission the `sp` field lacks `403 AuthorizationPermissionMismatch`, and a user delegation SAS (`skoid`)
      `403 AuthenticationFailed`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

- Filed by BL-423 (ADR-0109): BL-424 builds bearer and anonymous only; this adds the HMAC logins.
- Plan (2026-11-05): a new `AzureStorageScope` on `HttpAuthenticationRequest` (Abstractions), as BL-420's
  `AwsSigV4Service` is, makes the session judge Azure's methods whatever `--auth` says:
  `HttpAuthenticationSession.JudgeAzureStorageAsync` with `AzureStorageVerifier` (Bearer, `SharedKey`,
  `SharedKeyLite`, SAS). The server's `AzureLoginJudge` fills the scope and maps each
  `HttpAuthenticationRefusal` to decision 3.4's code; it still references neither `Surl.Authentication` nor an
  account. Eight refusals added: `MissingRequestDate`, `SignatureExpired`, `SignatureNotYetValid`,
  `AddressNotAllowed`, `ProtocolNotAllowed`, `UnservedSignatureKind`, `UnreadableSignatureFields`,
  `PermissionNotGranted`.
- Choices (defaults taken, to be recorded in ADR-0109 by BL-842, since Documentation is outside this task's
  `touches`):
  - SAS permission letters per operation: listing `l`, read `r`, delete `d`, Data Lake append `a` or `w`, any
    other write `w`. `c` alone never grants a write: the login is judged before the server knows whether the
    name exists, and serving an overwrite on `c` would serve what Azure refuses.
  - Service SAS `sr=c`, `b` and `d` (with `sdd` from 1 to the path's depth) are served; `bs`, `bv`, or a
    signed resource the request does not address, is `UnservedSignatureKind` (`403 AuthenticationFailed`).
    The canonicalized resource is built from the request, so a signature for another resource never verifies.
  - `AuthenticationErrorDetail` is a fixed sentence per refusal; an unknown account reads `Signature did not
    match.`, as a wrong signature does, so a peer cannot probe which accounts exist (decision 3.4).
  - An account whose password is not base64 text has no key: Shared Key and SAS naming it find the dummy, as an
    unknown account does (ADR-0100 decision 4.1 makes the password the key's base64).
  - The query is read with `+` as a space, as the server's `AzureBlobTarget` already reads it.
  - A malformed `SharedKey` header and another scheme are refused after the refusal delay with no login note
    (nothing was checked); no credentials at all is refused undelayed, as S3's are.
  - Credentials are checked even under `--allow-anonymous` for every Azure method, so a bad bearer token under
    anonymous is now `401 InvalidAuthenticationInfo`. That is half of BL-725's first criterion; BL-725 still
    owns its named test and the path-style account segment. Until BL-725, a SAS sent to an IP-literal or
    `localhost` host names that host's first label as its account, and is refused as an unknown account.
- Expected bytes: every signature in `AzureStorageVerifierTests` is made in the test over the string to sign
  written as "Authorize with Shared Key", "Create a service SAS" and "Create an account SAS" give it (the Get
  Container Metadata example's string for Blob), never from the library's builder and never from the Curl port.
- Coverage (2026-11-05, cobertura from the two test projects): every line and branch of the new and changed
  files in `Surl.Authentication`, `Surl.Protocol.Abstractions` and the Azure server is covered. The one partial
  branch reported, `HttpAuthenticationSession.cs` line 177 (`AnswerCheckAsync`'s switch), is in code this task
  did not change.
- Tests: `AzureStorageVerifierTests` (Authentication, 79), `AzureLoginJudgeTests` (Azure, 30); Authentication
  1124 and Azure 307 tests pass; `dotnet build -warnaserror` clean; every fast test project green.
- Filed BL-842 (record these choices in ADR-0109 and the conformance matrix).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Azure Storage server accepts Shared Key, SharedKeyLite, service and account SAS, each refusal with ADR-0109 decision 3.4's code
