---
id: BL-1719
title: Refuse a SigV4 SignedHeaders list that repeats a header name
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1719 — Refuse a SigV4 SignedHeaders list that repeats a header name

## Goal

A Signature Version 4 request whose `SignedHeaders` names a header twice is refused `MalformedCredentials` before any
canonical request is built, so a request head cannot make the server build text far larger than the head.

## Context

- Found while doing BL-1702, by reading the code; not yet reproduced by a test.
- `AwsSigV4Authorization.TryCreate` (`Foundation/Surl.Authentication.UnitLibrary`) takes `signedHeaders.Split(';')`
  as it comes, and `AwsSigV4CanonicalRequest.BuildHeaders` writes every value of a named field once per time the name
  appears. With the default `--max-request-head` (102400 bytes), one field of about 50 000 bytes named about 25 000
  times asks for over a billion characters. The canonical request is built for an unknown access key too, so no
  account is needed. Since BL-1702 the S3 server also sends a configured account's canonical request back, uncut.
- `SignatureV4Check.VerifySignature` is shared with Cloud Storage's `GOOG4-HMAC-SHA256`, and presigned URLs
  (`X-Amz-SignedHeaders`) read the same record, so one check in `TryCreate` covers all three.
- AWS's canonical request lists each signed header name once, lower-case and sorted. Check the recorded fixtures
  `Foundation/Surl.Authentication.UnitTests/Fixtures/aws-sigv4-*` (a request sending one header twice) so nothing
  pinned upstream curl sends is refused; measure with `Record-CurlExchange.ps1` if no fixture shows it.
- Compare names ignoring case, since `BuildHeaders` matches fields that way.
- Record the rule as an amendment of ADR-0043.

## Acceptance criteria

- [ ] Named tests: a `SignedHeaders` list repeating a name, in the same or another case, is refused
      `MalformedCredentials`, in the `Authorization` form, the presigned form and the `GOOG4-HMAC-SHA256` form.
- [ ] Named test: every recorded `aws-sigv4-*` fixture request is still judged as before, among them one sending a
      header twice.
- [ ] An ADR-0043 amendment states the rule, its reason and the curl evidence.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
