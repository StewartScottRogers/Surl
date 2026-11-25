---
id: BL-1702
title: Write CanonicalRequest and StringToSign in S3's SignatureDoesNotMatch error
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1701, BL-1698]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1702 — Write CanonicalRequest and StringToSign in S3's SignatureDoesNotMatch error

## Goal

The S3 server's `403 SignatureDoesNotMatch` error body carries `<StringToSign>` and `<CanonicalRequest>` elements
holding what the server computed, as AWS S3's does.

## Context

- Source: Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-25T1430-Surl-bug-report.md`, item 4
  (Low), against Surl v0.1.8 `9f8005dff`. Repro, copied from the report: the server of item 2
  (`surl --directory store --cert server.pem --key server.key --user K:S s3://127.0.0.1:18443/`, `store/photos/hello.txt`
  present); `curl ... --aws-sigv4 aws:amz:us-east-1:s3 -u K:wrong https://photos.s3.us-east-1.amazonaws.com/hello.txt`
  gives 403 with `<Code>SignatureDoesNotMatch</Code>`, but no `<CanonicalRequest>` or `<StringToSign>` elements.
  Expected: both, as AWS's error has them.
- BL-1701 makes the refused verdict carry `SignatureMismatch` (`AwsSigV4SignatureMismatch`, with the
  `x-amz-security-token` value already written as `****`). Its Context holds the decision and why; this task
  records it in ADR-0108 and writes the elements.
- S3 is an extended server (ADR-0083): judged by AWS's S3 documentation, not a curl recording.
- Where: `S3Login.ErrorOf` (`Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/S3Login.cs`) maps the
  refusal to `S3Error.SignatureDoesNotMatch`; the error must also take the verdict's mismatch (from
  `JudgeHeadAsync` and `JudgeBodyAsync`). `S3ErrorBody.cs` writes `Code`, `Message`, the code's extra element,
  `Resource`, `RequestId`; add `StringToSign` then `CanonicalRequest` after `Message` and before `Resource`,
  each XML-escaped (`&`, `<`, `>`) with the same escaping helper as `Resource`, not cut (a request head is
  already bounded by `--max-request-head`). A `HEAD` error still has no body (ADR-0108).
- BL-1698 also edits ADR-0108 and the S3 library; it is a dependency only so the two amendments are numbered in order.
- Stand-in: no store, no event change; the test author sees what the server signed.

## Acceptance criteria

- [ ] Regression test for this report in `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests/S3LoginExchangeTests.cs`:
      `GET /hello.txt` on `photos.s3.us-east-1.amazonaws.com` signed with a wrong secret is answered `403` whose body
      has `<Code>SignatureDoesNotMatch</Code>`, `<StringToSign>` and `<CanonicalRequest>` equal (after XML unescaping)
      to the strings the test computes for that request.
- [ ] Named tests in `S3ErrorBodyTests.cs`: element order `Code`, `Message`, `StringToSign`, `CanonicalRequest`,
      `Resource`, `RequestId`; a canonical request holding `&` and `<` is escaped; every other error has neither element.
- [ ] Named test: a `HEAD` refused `SignatureDoesNotMatch` has no body; a signed `x-amz-security-token` shows as
      `x-amz-security-token:****` in `<CanonicalRequest>`.
- [ ] `Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md` has an amendment (BL-1702) that
      reverses the "not sent" sentence and the rejected alternative, with BL-1701's reasons and the security-token
      exception, naming this report.
- [ ] `dotnet build Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary -warnaserror` is clean, the fast tests
      are green with no `TestCategory=Integration` test needed, and the library stays at 100% line and branch coverage.

## Notes

- AWS's responses carry more elements (`AWSAccessKeyId`, `SignatureProvided`, `StringToSignBytes`,
  `CanonicalRequestBytes`). They are out of scope; if the lane finds them in AWS's documentation it files a follow-up.
- From BL-1701, for the ADR-0108 amendment (its Notes say why): `****` is written for every signed header ADR-0182
  withholds (`Authorization`, `Proxy-Authorization`, the API-key fields and `x-amz-security-token`), not the security
  token alone; and the accepted limit that the string to sign's hash lets whoever holds both test guesses of a
  withheld value, which matters only for a guessable one.

## Log

- 2026-11-25: Created.
