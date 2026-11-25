---
id: BL-1700
title: Send the session's challenges with a chosen HTTP 401 error
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1699]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions/ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md]
requirement: none
created: 2026-11-25
completed:
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

- [ ] Regression test for this report in `Protocols/Http/Surl.Protocol.Http.UnitTests/HttpRequestAnswererTests.Faults.cs`:
      a chosen-error rule with reply `401` on a request let in by a valid Bearer login is answered
      `401 Unauthorized` with the session's challenge values as `WWW-Authenticate` fields, in order.
- [ ] Named tests: a session offering no challenge gives a `401` with no `WWW-Authenticate` and the exchange-log note
      of decision 2; a chosen `403` carries no `WWW-Authenticate`; a chosen `401` on a request with an unread body
      still closes the connection with the challenges.
- [ ] `Documentation/Planning/Decisions/ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md`
      has an amendment (BL-1700) stating decisions 1 to 3 and citing RFC 9110 section 11.6.1 and this report.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, the fast tests are green
      with no `TestCategory=Integration` test needed, and the library stays at 100% line and branch coverage.

## Notes

- RTSP's chosen error (`RtspRequestResponder`, ADR-0134 decision 2) has the same shape; if a chosen RTSP `401`
  also goes without a challenge, the lane files a follow-up task for it rather than widening this one.

## Log

- 2026-11-25: Created.
