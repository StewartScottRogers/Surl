---
id: BL-122
title: Verify AWS Signature Version 4 in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-118]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Conformance.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-122 — Verify AWS Signature Version 4 in Surl.Authentication

## Goal

`Surl.Authentication` verifies the AWS Signature Version 4 `Authorization` header upstream
curl 8.21.0 sends for `--aws-sigv4 <provider1[:provider2[:region[:service]]]> -u
key:secret`, with the configured access key and secret ADR-0032 decision 2 names, and a
request-time window from an injected `TimeProvider`.

## Context

FR-014 (it lists `--aws-sigv4`); ADR-0032 (BL-100) decisions 2, 3 (SigV4 is not a plain-text
secret), 4 and 8. Built on BL-110 and BL-118 (composed HTTP server and conformance harness;
this task adds a method in `Surl.Authentication` and a conformance test only).

- curl's `--aws-sigv4` documentation: https://curl.se/docs/manpage.html#--aws-sigv4
  (curl 8.21.0). It signs with `x-amz-date` (or `x-<provider>-date`), `host`, and the
  headers it names in `SignedHeaders`; the canonical request and string to sign follow AWS's
  SigV4 specification. HMAC-SHA256 and SHA-256 are in the BCL.
- Measure with `Record-CurlExchange.ps1` and the pinned reference build:
  `-sS --aws-sigv4 aws:amz:us-east-1:s3 -u AKIDEXAMPLE:secret http://127.0.0.1:P/x`, the
  same with `-X PUT -d body`, and with a query string; save fixtures with a `README.md`.
  The recorded date header fixes the time a test's fake `TimeProvider` returns.
- What SigV4 challenge (if any) a `401` carries is ADR-0032 decision 4's; curl signs the
  first request without waiting for one.

## Acceptance criteria

- [ ] Tests replay each measured request and prove the signature verifies for the configured
      key and secret at the recorded time, and is refused for a changed method, path, query,
      signed header, body hash, a wrong secret, an unknown key, and a date outside ADR-0032's
      window.
- [ ] An `[TestCategory("Integration")]` conformance test proves
      `curl -sS --aws-sigv4 aws:amz:us-east-1:s3 -u <key>:<secret> http://.../file` exits 0
      with the file's bytes and a wrong secret gets the measured exit code.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage kept; no method exceeds complexity 10.
- [ ] `Surl.Authentication.UnitLibrary/CLAUDE.md` names AWS Signature Version 4 among the
      methods it holds.

## Notes

## Log

- 2026-09-29: Created.
