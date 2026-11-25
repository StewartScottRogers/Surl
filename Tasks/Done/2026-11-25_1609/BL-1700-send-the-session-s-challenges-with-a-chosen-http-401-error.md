---
id: BL-1700
title: Send the session's challenges with a chosen HTTP 401 error
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1699]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions/ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1700 — Send the session's challenges with a chosen HTTP 401 error

## Goal

A fault rule or `Surl.Api` response override that makes the http server answer `401` sends the
`WWW-Authenticate` challenges the server's own `401` would send on that connection, as RFC 9110 section 11.6.1
requires.

## Context

- Source: Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-25T1430-Surl-bug-report.md`, item 3
  (Low), against Surl v0.1.8 `9f8005dff`. Repro, copied from the report: Surl.Api, an `https://` server with
  accounts and an override that returns `SurlFault.ProtocolError("401")` for a path (recipe
  `http-expire-tokens-with-a-response-override`, case `answer`). `curl -D - https://127.0.0.1:P/<path>` shows
  `HTTP/1.1 401` with no `WWW-Authenticate` line. Expected: at least one challenge, such as the server's own
  `Bearer realm=...`.
- Where: `Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.Faults.cs`, `AnswerChosenErrorAsync`,
  passes the verdict's `challenges` to `EmptyAnswer`/`Refuse`; on a request let in they are empty. HTTP/1.x,
  HTTP/2 and HTTP/3 all answer through `HttpRequestAnswerer`, so one change covers them.
- Decision (Claude under Stewart's delegation; recorded by this task as an ADR-0134 amendment):
  1. When the chosen status is `401` and the verdict carried no challenge, the answer carries
     `authenticationSession.CreateChallenges()` (BL-1699), one `WWW-Authenticate` field per value, in order.
  2. When the session offers none (no account, or only plaintext methods on a plain connection without
     `--allow-plaintext-auth`), the `401` goes without one, as there is no method a client could answer, and the
     exchange log notes `fault rule <id> chose 401, and the server offers no challenge on this connection`.
  3. Other chosen statuses are unchanged; a `407` is not given `Proxy-Authenticate` (the http server is not a proxy).
  Upstream curl only reads the field when it authenticates, so the exit codes ADR-0134 measured are unchanged;
  no new recording is needed.
- Stand-in: the `FaultAppliedEvent` the rule publishes is unchanged; the test sees the challenge in the response.

## Acceptance criteria

- [x] Regression test for this report in `Protocols/Http/Surl.Protocol.Http.UnitTests/HttpRequestAnswererTests.Faults.cs`:
      a chosen-error rule with reply `401` on a request let in by a valid Bearer login is answered
      `401 Unauthorized` with the session's challenge values as `WWW-Authenticate` fields, in order.
- [x] Named tests: a session offering no challenge gives a `401` with no `WWW-Authenticate` and the exchange-log note
      of decision 2; a chosen `403` carries no `WWW-Authenticate`; a chosen `401` on a request with an unread body
      still closes the connection with the challenges.
- [x] `Documentation/Planning/Decisions/ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md`
      has an amendment (BL-1700) stating decisions 1 to 3 and citing RFC 9110 section 11.6.1 and this report.
- [x] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, the fast tests are green
      with no `TestCategory=Integration` test needed, and the library stays at 100% line and branch coverage.

## Notes

- RTSP's chosen error (`RtspRequestResponder`, ADR-0134 decision 2) has the same shape; if a chosen RTSP `401`
  also goes without a challenge, the lane files a follow-up task for it rather than widening this one.
  It does: filed as BL-1724.
- Plan: `HttpRequestAnswerer.Faults.cs` (`WwwAuthenticateValuesForChosenError`) asks the session's
  `CreateChallenges()` for a chosen `401`; no exit-code or public-surface change.
- Decision 1 refined after review (ADR-0134 Amendment 1, D1): a let-in verdict's `WWW-Authenticate` values are
  only login success tokens (Negotiate's final token), never challenges, so a chosen `401` always sends the
  session's challenges in their place, rather than only "when the verdict carried no challenge".
- Decision 2's "(no account, ...)" is not what the code does: the session offers the methods `--auth` accepts
  whatever the accounts, so with no account a chosen `401` still carries the Digest challenges. The amendment
  states the code's behaviour.
- `ConnectionBoundLogins` compares the status code, not the whole `HttpStatus`, so `401 token expired` offering
  only NTLM or Negotiate is reset over HTTP/2 and HTTP/3 like the server's own `401`.
- Pinned curl 8.21.0 handled canned replies modelled on the answer cleanly (`--oauth2-bearer`: 1 request, exit 0;
  with `--fail` 22; `-u --anyauth`: 2 requests, exit 0). No fixtures added.
- Added `Documentation/Planning/Decisions/README.md` to `touches` to keep ADR-0134's index row true; no task in
  Doing named it.
- Gates on 2026-11-25: build 0 warnings, format clean, fast tests 28996 passed, 0 failed, 3 skipped;
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary` 0 failing members. One earlier full run had
  one failure in the MQTT 5 tests, which this change does not reach: filed as BL-1726. Other follow-ups:
  BL-1725 (proxy `407`), BL-1727 (`--aihelp`).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A chosen HTTP 401 now carries the challenges the connection's own 401 would offer, over HTTP/1.x, HTTP/2 and HTTP/3.
