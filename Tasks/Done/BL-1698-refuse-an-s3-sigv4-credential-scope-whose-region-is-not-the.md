---
id: BL-1698
title: Refuse an S3 SigV4 credential scope whose region is not the regional endpoint's
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md, Documentation/Conformance/Extended/S3.md, Application/Surl.Cli.UnitLibrary/AiHelpProse.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1698 — Refuse an S3 SigV4 credential scope whose region is not the regional endpoint's

## Goal

An S3 request signed with AWS Signature Version 4 whose credential scope names a region other than the one in a
regional AWS `Host` (`s3.<region>.amazonaws.com` or `<bucket>.s3.<region>.amazonaws.com`) is answered
`400 AuthorizationHeaderMalformed`; every other `Host` still accepts any region.

## Context

- Source: Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-25T1430-Surl-bug-report.md`, item 2
  (Medium), against Surl v0.1.8 `9f8005dff`. Repro, copied from the report:
  - `surl --directory store --cert server.pem --key server.key --user K:S s3://127.0.0.1:18443/`
    (`store/photos/hello.txt` present; the certificate names `*.s3.us-east-1.amazonaws.com`).
  - `curl --cacert server.pem --connect-to ::127.0.0.1:18443 --aws-sigv4 aws:amz:eu-west-1:s3 -u K:S https://photos.s3.us-east-1.amazonaws.com/hello.txt`
    gives 200, as `aws:amz:us-east-1:s3` does.
  - Expected: 400 `AuthorizationHeaderMalformed`, as AWS answers a scope whose region is not the endpoint's.
- S3 is an extended server (ADR-0083): it is judged by the protocol's specification, not by a curl recording.
  AWS's SigV4 documentation ("Elements of an AWS API request signature", credential scope) says the scope's region
  is the region the request is sent to, and AWS S3 refuses a mismatch with `AuthorizationHeaderMalformed` (400).
  The lane cites the AWS page it used in the ADR amendment.
- Today's behaviour is deliberate: `Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md`
  says "Region is never checked against anything: the server is every region at once" (decision on virtual
  hosting) and "Any region in the scope is accepted" (decision 4). Decision (Claude under Stewart's delegation,
  recorded by the lane as an ADR-0108 amendment): when the client's own `Host` names a region, the endpoint has
  one, and a stand-in that accepts a wrong scope hides the bug a test exists to catch. Any `Host` that names no
  region (an IP literal, `localhost`, the listen URL's DNS name, `s3.amazonaws.com`) stays every region at once, so
  no current path-style use changes.
- Where: `S3VirtualHost.cs` already parses the regional AWS host forms (`RegionalAwsBucket`); add a way to read the
  region label from both forms. The scope's region is the third `/`-separated part of `Credential=` in an
  `AWS4-HMAC-SHA256` `Authorization` or of the `X-Amz-Credential` query parameter of a presigned URL. Check it in
  the S3 library (for example in `S3Login.JudgeHeadAsync`, before the authentication session judges), so no
  contract changes: the S3 server references neither `Surl.Authentication` nor its parser. A credential that
  does not parse is left for the session, which already answers `AuthorizationHeaderMalformed` (ADR-0108
  decision 4.3, Amendment 9 for presigned URLs). Use `S3Error.AuthorizationHeaderMalformed` with its existing
  fixed `Message` (ADR-0108: messages are fixed per code); both the header and the presigned form answer it,
  as Amendment 9 already maps malformed presigned credentials.
- Stand-in: no store change, nothing is written; the refusal is published on the event stream as every S3
  refusal already is. A test that signs for the wrong region now sees the 400 real S3 would send.

## Acceptance criteria

- [x] Regression test for this report in `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests` (in
      `S3LoginExchangeTests.cs` or `S3LoginTests.cs`): `GET /hello.txt` with `Host: photos.s3.us-east-1.amazonaws.com`
      and a valid signature over the scope `…/eu-west-1/s3/aws4_request` is answered `400` with
      `<Code>AuthorizationHeaderMalformed</Code>` and the object is not sent.
- [x] Named tests: the same request with scope `us-east-1` is `200`; with `Host: s3.us-east-1.amazonaws.com` and a
      path-style target the mismatch is `400 AuthorizationHeaderMalformed`; a presigned URL whose `X-Amz-Credential`
      names `eu-west-1` on a `us-east-1` host is `400 AuthorizationHeaderMalformed`.
- [x] Named tests: with `Host: 127.0.0.1:<port>`, `localhost` and `s3.amazonaws.com`, a scope naming any region is
      still accepted.
- [x] `Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md` has a new amendment (BL-1698) that
      replaces "Region is never checked" with the rule above, cites the AWS document, and names this report.
- [x] `dotnet build Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary -warnaserror` is clean, the fast
      tests are green with no `TestCategory=Integration` test needed, and the S3 library stays at 100% line and
      branch coverage.

## Notes

- Added to `touches` (2026-11-25): `Documentation/Conformance/Extended/S3.md`, whose rows `S3:Surl-Addressing-2`
  and `S3:SigV4-Scope-1` this change makes false, and `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`, whose
  `s3` paragraph must describe the new refusal in the same diff (root `CLAUDE.md`). No task in `Doing` names either.
- Plan (protocol-architect): `S3VirtualHost.RegionOf` reads the region of a regional AWS `Host`; new
  `S3CredentialScope.RegionOf` reads the scope's region from `Authorization` or `X-Amz-Credential`;
  `S3Login.JudgeHeadAsync` refuses a mismatch before the session judges (no secret consulted, so no failed-login
  delay, as Amendment 1 point 4). Anything the library cannot parse is left to the session. The region is the third
  `/` part from the end, because the session allows `/` in an access key. No exit code changes.
- The AWS page that states the mismatch rule is "Troubleshoot Signature Version 4 signing"
  (`IAM/latest/UserGuide/reference_sigv-troubleshooting.html`, "Credential scope errors"); "Elements of an AWS API
  request signature" gives only the scope's shape.

- Outcome (2026-11-25): ADR-0108 Amendment 11 records the rule. Solution build `-warnaserror` and format check clean; fast
  tests 29010 passed, 0 failed, 3 skipped (S3: 537); S3 library 100% line and 100% branch coverage. Review: no
  findings. Conformance (pinned upstream curl 8.21.0, `--s3-plaintext`; TLS not exercised): 200 for the right region,
  400 for a wrong one, header and presigned forms. Other `<X>4-HMAC-SHA256` schemes are not region-checked
  (Amendment 11, point 3).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A regional AWS Host with a credential scope for another region is answered 400 AuthorizationHeaderMalformed; every other Host accepts any region
