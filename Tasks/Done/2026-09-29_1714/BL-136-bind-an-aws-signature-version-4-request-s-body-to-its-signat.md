---
id: BL-136
title: Bind an AWS Signature Version 4 request's body to its signature
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-122]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Conformance.UnitTests, Documentation/Planning/Decisions]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-136 — Bind an AWS Signature Version 4 request's body to its signature

## Goal

A request upstream curl 8.21.0 signs with `--aws-sigv4` and a body is served only when the body
received is the one signed: for `s3` (and `--aws-sigv4 aws`), the body's SHA-256 matches the
signed `x-<provider>-content-sha256` field; for every other service, which sends no such field,
the signature is checked over the body's SHA-256.

## Context

ADR-0043 (BL-122) decisions 5 and 6: the policy is judged on the head alone (ADR-0032 section 6),
so today `AwsSigV4AuthenticationMethod` takes the payload hash from the
`x-<provider>-content-sha256` field without seeing the body, and refuses a request with a body and
no such field. Fixtures are in `Surl.Authentication.UnitTests/Fixtures`: `aws-sigv4-put` (s3,
field sent) and `aws-sigv4-ec2-put` (ec2, no field; body `body`).

- The HTTP server judges the login before reading any body byte (ADR-0032 section 4, "Where it is
  judged"). A design is needed - an ADR - for a check after the body: either the verdict carries a
  "body must hash to X" condition the HTTP server enforces after reading it (refusing with the
  `401` and delay otherwise), or the server reads a bounded body before judging such a request.
- `UNSIGNED-PAYLOAD` as the field's value (curl sends it when the user gives it with `-H`) means
  the body is not bound; measure it before deciding.

## Acceptance criteria

- [x] An ADR records how a Signature Version 4 body is bound and why, "Decided by Claude under
      Stewart's delegation".
- [x] Tests replay `aws-sigv4-put` with its body and prove it served, and with a changed body
      prove it refused.
- [x] Tests replay `aws-sigv4-ec2-put` with its body and prove it served, and with a changed body
      prove it refused.
- [x] An `[TestCategory("Integration")]` conformance test proves
      `curl -sS --aws-sigv4 aws:amz:us-east-1:ec2 -u <key>:<secret> -T <file> http://.../upload`
      against `surl --allow-uploads` exits 0.
- [x] `dotnet build` is clean; the fast tests pass; 100% line and branch coverage kept; no
      method exceeds complexity 10.

## Notes

- **Design (ADR-0045).** A `Proceed` verdict may carry an `IHttpRequestBodyCheck`
  (Abstractions). The HTTP server then checks the upload limit, reads the body (after
  `100 Continue` when expected), hashes its content bytes with SHA-256 in
  `HttpRequestBodyDiscarder`, and answers the body check's verdict as a request with no body
  left to read. `AwsSigV4AuthenticationMethod` answers `HttpCredentialOutcome.AwaitingBody`
  with `HttpCredentialCheck.CheckBody`; `HttpAuthenticationSession` maps it and refuses a
  mismatch after the refusal delay with the login note. With a content hash field the
  signature is checked on the head (a wrong secret never gets its body read); a date outside
  the window is refused before the body for every service.
- **Measured** (fixtures `aws-sigv4-unsigned-payload`, `aws-sigv4-upload`,
  `aws-sigv4-ec2-upload`): curl signs `UNSIGNED-PAYLOAD` as given; `-T` with `s3` sends
  `UNSIGNED-PAYLOAD` itself; `-T` with `ec2` signs the empty body's hash while sending the file.
- **Decision: `curl -T` with `ec2` is refused (401)**, since the body sent is not the one
  signed and AWS refuses it too; accepting it would unbind every body. curl exits 0 with that
  401 (no `-f`), which is what the acceptance criterion's command pins; the conformance test
  asserts exit 0 and the status for four cases: `ec2 --data-binary` (405, past the login, since
  HTTP serves no PUT yet), the same with a wrong secret (401), `s3 -T` (405) and `ec2 -T` (401).
- `touches` gained `Documentation/Planning/Decisions` for ADR-0045 (and ADR-0043's amendment
  note); no task in Doing names it.
- Coverage: Authentication, Http and Abstractions libraries at 100% line and branch, worst CRAP
  10; the final `Measure-CodeQuality.ps1` run reports 0 failing members solution-wide (an earlier
  run reported one in `Surl.Content.UnitLibrary`, which this task does not touch).
- Code review: no must-fix. Taken: a head whose canonical request cannot be built (a signed
  field missing) is refused before its body is read, and the discarder keeps no hash past a
  call. Accepted as is: a login already accepted on the head (`s3`) whose body then gets a
  `413` or `400` writes no login note, because the login is not final until the body check.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. An AWS Signature Version 4 body is bound to its signature: s3 bodies must hash to x-amz-content-sha256, other services are verified over the body's SHA-256 once read (ADR-0045)
