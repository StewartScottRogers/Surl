---
id: BL-430
title: Check GOOG4-HMAC-SHA256 signatures in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-429]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-430 — Check GOOG4-HMAC-SHA256 signatures in Surl.Authentication

## Goal

`Surl.Authentication` checks a Cloud Storage V4 HMAC `Authorization: GOOG4-HMAC-SHA256 Credential=…,
SignedHeaders=…, Signature=…` header against an HMAC key in the accounts file. It uses the same request-binding
rules as the existing SigV4 check, and is reachable through the `Surl.Protocol.Abstractions` contracts.

## Context

- Decision: BL-429's ADR, decision 3.
- Model: `AwsSigV4AuthenticationMethod`, `AwsSigV4CanonicalRequest`, `AwsSigV4Calculation` and `AwsSigV4Account`
  (ADR-0043, ADR-0045). Share the canonical-request code with the AWS variant, parameterised by the algorithm
  name, date header, content-hash header and scope terminator, rather than copying it. The AWS bytes must not change.
- Specification: Cloud Storage "V4 signing process" and "Canonical requests": the `GOOG4-HMAC-SHA256` string to
  sign, the `goog4_request` scope, `x-goog-date`, `x-goog-content-sha256` and `UNSIGNED-PAYLOAD`.

## Acceptance criteria

- [ ] A named test checks a signature worked by hand from the documentation's steps (the inputs and every
      intermediate value in the test's comments) and accepts it at its date with a fixed `TimeProvider`.
- [ ] Named tests show a bad signature, an unknown access ID and a date outside the allowed skew each refused,
      with the verdict the ADR maps to an XML error code.
- [ ] Every existing `AwsSigV4*` test passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Authentication` keeps 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
