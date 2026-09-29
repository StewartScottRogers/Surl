---
id: BL-136
title: Bind an AWS Signature Version 4 request's body to its signature
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-122]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Conformance.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
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

- [ ] An ADR records how a Signature Version 4 body is bound and why, "Decided by Claude under
      Stewart's delegation".
- [ ] Tests replay `aws-sigv4-put` with its body and prove it served, and with a changed body
      prove it refused.
- [ ] Tests replay `aws-sigv4-ec2-put` with its body and prove it served, and with a changed body
      prove it refused.
- [ ] An `[TestCategory("Integration")]` conformance test proves
      `curl -sS --aws-sigv4 aws:amz:us-east-1:ec2 -u <key>:<secret> -T <file> http://.../upload`
      against `surl --allow-uploads` exits 0.
- [ ] `dotnet build` is clean; the fast tests pass; 100% line and branch coverage kept; no
      method exceeds complexity 10.

## Notes

## Log

- 2026-09-29: Created.
