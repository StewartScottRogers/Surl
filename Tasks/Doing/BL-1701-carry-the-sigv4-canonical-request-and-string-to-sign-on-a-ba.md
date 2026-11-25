---
id: BL-1701
title: Carry the SigV4 canonical request and string to sign on a BadSignature verdict
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1701 — Carry the SigV4 canonical request and string to sign on a BadSignature verdict

## Goal

A refused AWS Signature Version 4 verdict whose `Refusal` is `BadSignature` carries the canonical request and the
string to sign the server computed, so the S3 server can write them into its `SignatureDoesNotMatch` error (BL-1702).

## Context

- Source: Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-25T1430-Surl-bug-report.md`, item 4
  (Low), against Surl v0.1.8 `9f8005dff`. Repro, copied from the report: the server of item 2
  (`surl --directory store --cert server.pem --key server.key --user K:S s3://127.0.0.1:18443/`);
  `curl ... --aws-sigv4 aws:amz:us-east-1:s3 -u K:wrong https://photos.s3.us-east-1.amazonaws.com/hello.txt` gives
  403 with `<Code>SignatureDoesNotMatch</Code>` but no `<CanonicalRequest>` or `<StringToSign>` elements.
  Expected: both, as AWS's error has them.
- ADR-0108 rejected sending them ("a debugging aid that tells a probing peer how the request was canonicalised").
  Decision (Claude under Stewart's delegation; BL-1702 records it as an ADR-0108 amendment): send them. Both are
  computed only from the client's own request and the public SigV4 algorithm and hold no secret of the server's;
  a stand-in's job is to let a test author see why a signature differs, which is what AWS's elements are for.
  One exception keeps ADR-0182 whole: the value of a signed `x-amz-security-token` header, or an
  `X-Amz-Security-Token` query parameter, is written as `****` in the carried canonical request, because the sent
  bytes reach the `-v` log unredacted and ADR-0182 withholds that token on receipt. The string to sign is the
  genuine one (its hash line is of the real canonical request).
- Where:
  - `Foundation/Surl.Protocol.Abstractions.UnitLibrary`: a new record `AwsSigV4SignatureMismatch(string
    CanonicalRequest, string StringToSign)` and an optional last parameter
    `AwsSigV4SignatureMismatch? SignatureMismatch = null` on `HttpAuthenticationVerdict`, with doc comments.
  - `Foundation/Surl.Authentication.UnitLibrary`: `AwsSigV4AuthenticationMethod` (line 195 refuses with
    `BadSignature`), `AwsSigV4CanonicalRequest`, `AwsSigV4Calculation`, and the session's mapping to the verdict
    (`HttpAuthenticationSession`, which builds refusals around line 230-280) fill it for the `Authorization`
    form, the presigned query form, and a header signature whose check waits for the body. `aws-chunked` chunk
    signature failures are out of scope.
- Stand-in: no store, no event change.

## Acceptance criteria

- [ ] Named tests in `Foundation/Surl.Authentication.UnitTests/AwsSigV4AuthenticationMethodTests.cs`: a request
      signed with a wrong secret is refused `BadSignature` and its verdict's `SignatureMismatch` holds the canonical
      request and string to sign of AWS's documented SigV4 "GET Object" example (`examplebucket`,
      `AKIAIOSFODNN7EXAMPLE`; the vectors `AwsSigV4ServiceSignatureTests.cs` already uses), byte for byte.
- [ ] Named tests in `AwsSigV4PresignedAndChunkedTests.cs`: the same for a presigned URL, and for a body-bound
      signature refused at the body check.
- [ ] Named test: a signed `x-amz-security-token` appears as `x-amz-security-token:****` in the carried canonical
      request, and the string to sign's last line is the SHA-256 of the real canonical request.
- [ ] Named tests: every other refusal, and every accepted verdict, has `SignatureMismatch` null.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` and
      `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` are clean, the fast tests of the
      whole solution are green, and both libraries stay at 100% line and branch coverage.

## Notes

- Four projects because the contract addition is a record and one optional parameter; the writing of the elements
  is BL-1702.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
