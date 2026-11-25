---
id: BL-1724
title: Send the session's challenges with a chosen RTSP 401 error
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1700]
touches: [Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests, Documentation/Planning/Decisions/ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1724 — Send the session's challenges with a chosen RTSP 401 error

## Goal

A fault rule that makes the RTSP server answer `401` sends the `WWW-Authenticate` challenges the connection's own
`401` would send.

## Context

- Found during BL-1700, which fixed the same gap in the HTTP server. Read from code on 2026-11-25, not yet
  reproduced by a test:
  `Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary/RtspRequestResponder.Faults.cs`, `AnswerLetInRequestAsync`, refuses
  a chosen error through `RefuseAsync`, and `ResponseHead` in `RtspRequestResponder.cs` adds only
  `wwwAuthenticateValues`, the let-in verdict's values. Those are empty, or at most a login success token, so a
  chosen `401` carries no challenge.
- RFC 9110 section 11.6.1: a server generating a `401` must send at least one challenge. RFC 2326 section 1.4
  applies HTTP authentication to RTSP, and section 12.44 lists `WWW-Authenticate`.
- Decision (Claude under Stewart's delegation; recorded by this task as an ADR-0134 amendment). It mirrors
  ADR-0134 Amendment 1 for decision 2:
  1. A chosen status `401`, whatever its reason phrase, carries the values of
     `authenticationSession.CreateChallenges()`, one `WWW-Authenticate` field per value, in order, in place of the
     let-in verdict's values (success tokens are not challenges).
  2. When the session offers none, the `401` goes without the field and the exchange log notes
     `fault rule <id> chose 401, and the server offers no challenge on this connection`.
  3. Every other chosen status is unchanged, `407` included: the RTSP server is not a proxy.
  The rest of the answer (`CSeq`, `Date`, `Server`, `Session`, connection kept, no body) is unchanged.
- The HTTP implementation to follow is `WwwAuthenticateValuesForChosenError` in
  `Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.Faults.cs`. The protocol libraries do not
  reference each other.
- `Protocols/Http/Surl.Protocol.Rtsp.UnitTests/UnitTestAuthenticationPolicy.cs`'s session does not implement
  `CreateChallenges`, so it needs a settable list of offered challenges.
- No new recording from pinned upstream curl 8.21.0 is needed: the field has the shape of the login `401` the
  fixture `no-login-401` already pins.
- Stand-in: no store changes; the rule publishes the same one `FaultAppliedEvent`, and a test seeds the fault with a
  `--fault` rule or its `Surl.Api` equivalent and sees the challenges in the response.

## Acceptance criteria

- [x] A test in `Protocols/Http/Surl.Protocol.Rtsp.UnitTests/RtspFaultTests.cs` shows a chosen `401` on a request
      let in with a success token is answered `RTSP/1.0 401 Unauthorized` with the session's challenges as
      `WWW-Authenticate` fields, in order, without the token, and the connection stays open.
- [x] A named test shows that a session offering no challenge gives a `401` with no `WWW-Authenticate` and the
      exchange-log note of decision 2.
- [x] A named test shows a chosen status other than `401` still sends the verdict's values and none of the session's
      challenges.
- [x] ADR-0134 has an amendment naming this task that states decisions 1 to 3, and its row in
      `Documentation/Planning/Decisions/README.md` says the RTSP chosen `401` carries the challenges.
- [x] `Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md`'s `RtspRequestResponder.Faults.cs` bullet and that
      file's doc comments say what a chosen `401` sends.
- [x] `dotnet build Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary -warnaserror` is clean, the fast tests are green
      with no `TestCategory=Integration` test needed, and the library stays at 100% line and branch coverage.

## Notes

- Delivered in the lane session without the `/feature` subagents: the task already holds the plan (the decision and
  the HTTP implementation to mirror), and the change is one private method in one library.
- Tests first: the three new test methods in `RtspFaultTests` (`ChosenUnauthorized_*`,
  `ChosenErrorOtherThanUnauthorized_*`) were run before the change; the two `401` methods failed (3 cases).
- `RtspRequestResponder.Faults.cs`, `WwwAuthenticateValuesForChosenError`, mirrors the HTTP method of the same name.
- Recorded as ADR-0134 Amendment 2. No new upstream curl recording, as the task's Context decides.
- Verified 2026-11-25: solution build `-warnaserror` 0 warnings, `dotnet format --verify-no-changes` passed, all fast
  tests green (RTSP 250 of 250), `Measure-CodeQuality.ps1` for the RTSP library 100% line, 100% branch, 0 failing.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A chosen RTSP 401 sends the session's WWW-Authenticate challenges, or none, noted in the exchange log, when it offers none.
