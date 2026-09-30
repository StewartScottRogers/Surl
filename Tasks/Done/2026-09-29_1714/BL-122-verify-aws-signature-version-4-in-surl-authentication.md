---
id: BL-122
title: Verify AWS Signature Version 4 in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-118]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Conformance.UnitTests, Surl.Console, Surl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
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

- [x] Tests replay each measured request and prove the signature verifies for the configured
      key and secret at the recorded time, and is refused for a changed method, path, query,
      signed header, body hash, a wrong secret, an unknown key, and a date outside ADR-0032's
      window.
- [x] An `[TestCategory("Integration")]` conformance test proves
      `curl -sS --aws-sigv4 aws:amz:us-east-1:s3 -u <key>:<secret> http://.../file` exits 0
      with the file's bytes and a wrong secret gets the measured exit code.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage kept; no method exceeds complexity 10.
- [x] `Surl.Authentication.UnitLibrary/CLAUDE.md` names AWS Signature Version 4 among the
      methods it holds.

## Notes

- Measured ten `aws-sigv4-*` fixtures from the pinned reference build (README in
  `Surl.Authentication.UnitTests/Fixtures`), beyond the three named: path and query encoding,
  `ec2` (no content hash field), `--aws-sigv4 aws` alone, another provider (`osc`), `-H` fields,
  and a wrong secret. The canonical query rule (decode, re-encode, `+` as space, sort by name then
  value) was found by replaying the recorded signature against candidate forms.
- Decisions are ADR-0043 (Decided by Claude under Stewart's delegation): any provider, region and
  service; the password as UTF-8 is the secret; a 15-minute window either way (AWS's skew
  allowance), inclusive; `host` and the date must be signed.
- `touches` grew by `Surl.Console` and `Surl.Console.UnitTests` (no task in Doing named them):
  the conformance criterion needs `surl` to compose the method and accept `--auth aws-sigv4`,
  which it refused with 2 until now. With every `--auth` word implemented, the "not available in
  this build" refusal and its test are gone. Also `Documentation/Planning/Decisions` for the ADR.
- The measured exit code for a wrong secret is 0 with an empty body (no `-f`); with `-f` it is 22.
  Both are conformance tests.
- Follow-up filed: BL-136 binds the body to the signature. The policy sees the head only
  (ADR-0032 section 6), so an `s3` write's body is not yet compared with its signed hash, and a
  write for another service (no hash field) is refused.
- Results: `Surl.Authentication.UnitTests` 432 passed (60 new for SigV4), all fast tests green;
  `Surl.Authentication.UnitLibrary` and `Surl.Console` at 100% line and branch, worst CRAP 10;
  the three conformance tests pass against the pinned build.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl verifies upstream curl's --aws-sigv4 signatures (any provider, 15-minute window); pinned curl logs in and a wrong secret is refused
