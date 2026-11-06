---
id: BL-432
title: Accept V4 HMAC signed requests on the Google Cloud Storage server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431, BL-430]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-432 — Accept V4 HMAC signed requests on the Google Cloud Storage server

## Goal

The Cloud Storage server accepts `GOOG4-HMAC-SHA256` signed requests through BL-430's check. It refuses
each failing signature with the XML error code BL-429's ADR names.

## Context

- Decision: BL-429's ADR, decision 3. The check is reached through the `Surl.Protocol.Abstractions`
  contracts, not by referencing `Surl.Authentication`.
- The signature covers the body's hash (`x-goog-content-sha256`), so a body is bound after it is read, as
  ADR-0045 does for SigV4.
- BL-429's ADR is ADR-0110 (2026-11-05): decision 3.4's table maps each refusal - `400 InvalidArgument`
  (malformed header or scope), `400 MissingSecurityHeader`, `403 InvalidAccessKeyId`, `403 SignatureDoesNotMatch`,
  `403 RequestTimeTooSkewed`, and `400 BadDigest` for a body not hashing to `x-goog-content-sha256`.

## Acceptance criteria

- [x] Named tests replay BL-430's hand-worked signed GET and a signed PUT and accept them. A bad signature
      gets `403 SignatureDoesNotMatch`, and a body whose hash differs from `x-goog-content-sha256` is refused as
      the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Built (2026-11-05): `GcsLoginJudge` sends an `Authorization` whose first token is `GOOG4-HMAC-SHA256` (any case)
  to the session with `AwsSigV4Service = "storage"` (`SignedService`), which picks BL-430's
  `GoogleV4HmacAuthenticationMethod`; `ErrorOf` maps each refusal through ADR-0110 decision 3.4. `GcsError` gains
  `InvalidAccessKeyId`, `MissingSecurityHeader`, `RequestTimeTooSkewed` and `SignatureDoesNotMatch` with the ADR's
  messages. A body check (ADR-0045) is finished by PUT Object from a SHA-256 kept on the way into the upload
  (`GcsHashingStream`), before `Content-MD5`/`x-goog-hash` and before commit, so a mismatch stores nothing; a
  folder PUT checks the empty body; every other request reads and discards its body first, as the S3 server does.
- Defaults taken (rule 1), each mirroring the S3 server (ADR-0108 decision 4.4), so no new ADR: a refusal reason
  decision 3.4 does not name is `403 AccessDenied`; `UnsupportedScheme` (only reachable if the session disagrees
  with the judge about the scheme) is `400 InvalidAuthentication`; the scheme alone with no credentials is still
  routed to the check, which answers it malformed (`400 InvalidArgument`) as the ADR's table says, rather than
  `InvalidAuthentication`.
- Tests: the GCS test project may not reference `Surl.Authentication` (`ProtocolIsolationTests`), so - as the S3
  server's tests do - the hand-written `UnitTestGcsAuthenticationPolicy` (renamed from
  `UnitTestBearerAuthenticationPolicy`, since it now judges signatures too) stands in for the session. The replayed
  bytes are genuine: BL-430's worked GET verbatim, and a signed PUT of `hello world` worked by hand in PowerShell
  with the same made-up key (every intermediate value in `GcsSignedRequests`; the GET's canonical-request hash
  `926f3db0…` reproduced first to check the method). End to end through the real check is BL-433's `surl` wiring.
- `touches` gained `Documentation/Conformance/Extended/GoogleCloudStorage.md`: its V4 rows named BL-432 as the
  pending cover and now name the tests. No task in Doing touches it.
- Coverage: the library is at 100% line and branch (218 tests).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The gs server accepts GOOG4-HMAC-SHA256 signed requests through the session's storage check, binds a signed body once read, and answers each refusal with ADR-0110 decision 3.4's error
