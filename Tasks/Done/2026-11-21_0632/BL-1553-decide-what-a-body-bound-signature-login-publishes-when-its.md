---
id: BL-1553
title: Decide what a body-bound signature login publishes when its body is never read
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0045-an-aws-signature-version-4-body-is-bound-after-it-is-read.md]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1553 — Decide what a body-bound signature login publishes when its body is never read

## Goal

An ADR-0045 amendment, marked "Decided by Claude under Stewart's delegation", says what `LoginJudgedEvent` (if
any) a Signature Version 4 or Cloud Storage V4 HMAC login publishes when its check waits for the body and the
server refuses the request without reading it, and the implementation it calls for is filed as tasks.

## Context

- Surly cookbook bug report 2026-11-21, item 4: on a `gs://` listener without `--allow-uploads`, an
  HMAC-signed `PUT` is refused and `get_events` shows no `login_judged`; a Bearer-token `PUT` does show one.
- Cause, in today's code:
  - `GcsLoginJudge.LogInAsync`
    (`Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/GcsLoginJudge.cs:59-70`)
    judges the head. A signed request with a body (no `x-goog-content-sha256`, or one checked on the head) comes
    back from `HttpAuthenticationSession.AnswerCheckAsync`
    (`Foundation/Surl.Authentication.UnitLibrary/HttpAuthenticationSession.cs:259-261`) as `Proceed` with a
    `BodyCheck` and **no** `CheckedLogin` ("a login waiting for the body checked nothing yet"), so
    `Published` (`:233-240`) publishes nothing. The event comes only when the body check finishes
    (`RequestBodyCheck`).
  - `GcsPutObject` (`GcsPutObject.cs:48`) then refuses `403 AccessDenied` for uploads off before reading the
    body, so the body check never runs and no event is ever published. `GcsRequestAnswerer.BodyReaders`
    (`GcsRequestAnswerer.cs:26`) lists PutObject as reading its own body, so `UnusedBodyRefusalAsync` (`:131-135`)
    does not discard and judge it either.
  - The same holds for the S3 server's writes refused before the body (`S3PutObject.cs:45-47`, and the
    multipart operations' `AllowUploads` checks) and for any HTTP server answer that refuses before reading a
    bound body. A Bearer token is judged whole on the head, which is why it shows an event.
- Rules in play: ADR-0124 decision 1 (every judged login publishes `LoginJudgedEvent`), ADR-0128 (which
  logins are "checked"), ADR-0045 (the body is bound after it is read; a stale date is refused before), ADR-0006
  section 2 (uploads off reads nothing). Options include: publishing at the head when the signature itself
  was verified there over a sent content hash (`SignatureV4Check.VerifyWithTheSentHash` names the account,
  `SignatureV4Check.cs:226`); reading and judging the body before an uploads-off refusal; or recording that
  no event is published and why. Secure by default: the choice must not let a peer learn more about an
  account than today.
- Stand-in: a test asserting "the client's login was judged" across `gs`, `s3` and `http` should see the same
  thing on each; the decision says what that is.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-0045-an-aws-signature-version-4-body-is-bound-after-it-is-read.md`
  has an amendment that decides, for a body-bound login refused before its body is read, whether a
  `LoginJudgedEvent` is published, when, and with what `is_accepted`, with the reasons.
- [x] The amendment says whether the answer the client gets (status, body, refusal delay) changes; if it does
  not, it says so.
- [x] The amendment lists each server it applies to (`gs`, `s3`, `http`, `az` if it binds bodies) and the
  file where each change lands.
- [x] One task per library and its `.UnitTests` twin is filed with `task-board.ps1 new`, each depending on
  this one and naming its tests, and the amendment names their IDs.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. ADR-0045 amendment 1 decides abandoned body-bound logins; BL-1561..1560 filed
