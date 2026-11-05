---
id: BL-430
title: Check GOOG4-HMAC-SHA256 signatures in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-429]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary/HttpAuthenticationRefusal.cs]
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

- **Lane 3 (2026-11-05).** The finished work is on branch factory/BL-430-lane-7-20261105-054539 (commits
  d1cc9bc7 and 79088bb2; that branch's copy of this file records the design, choices and coverage). It adds
  `HttpAuthenticationRefusal.MissingRequestDate` to `Surl.Protocol.Abstractions.UnitLibrary`: ADR-0110 3.4 answers
  a GOOG4 request with no `x-goog-date` and no `Date` `400 MissingSecurityHeader`, and no existing refusal says
  "no date" (reusing `MissingPayloadHash` would make the name lie). That file is now in `touches`. BL-734, in
  Doing, names `Foundation/Surl.Protocol.Abstractions.UnitLibrary`, so the task goes back to Backlog until it no
  longer overlaps. To resume: `git cherry-pick --no-commit d1cc9bc7 79088bb2` (applies cleanly on dcd40690), then
  build and run the fast tests.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Lane 9 could not integrate: push kept being refused. The work is on branch factory/BL-430-lane-9-20261105-054539; start with git cherry-pick --no-commit factory/BL-430-lane-9-20261105-054539 and fix it.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Lane 7 could not integrate: push kept being refused. The work is on branch factory/BL-430-lane-7-20261105-054539; start with git cherry-pick --no-commit factory/BL-430-lane-7-20261105-054539 and fix it.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Foundation/Surl.Protocol.Abstractions.UnitLibrary (HttpAuthenticationRefusal.MissingRequestDate), which BL-734 in Doing touches; finished work is on factory/BL-430-lane-7-20261105-054539
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Lane 5 could not integrate: push kept being refused. The work is on branch factory/BL-430-lane-5-20261105-054539; start with git cherry-pick --no-commit factory/BL-430-lane-5-20261105-054539 and fix it.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Lane 6 could not integrate: push kept being refused. The work is on branch factory/BL-430-lane-6-20261105-054539; start with git cherry-pick --no-commit factory/BL-430-lane-6-20261105-054539 and fix it.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Lane 8 could not integrate: push kept being refused. The work is on branch factory/BL-430-lane-8-20261105-054539; start with git cherry-pick --no-commit factory/BL-430-lane-8-20261105-054539 and fix it.
- 2026-11-05: Backlog -> Doing.
