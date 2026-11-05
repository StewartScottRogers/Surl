---
id: BL-432
title: Accept V4 HMAC signed requests on the Google Cloud Storage server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431, BL-430]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Named tests replay BL-430's hand-worked signed GET and a signed PUT and accept them. A bad signature
      gets `403 SignatureDoesNotMatch`, and a body whose hash differs from `x-goog-content-sha256` is refused as
      the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
