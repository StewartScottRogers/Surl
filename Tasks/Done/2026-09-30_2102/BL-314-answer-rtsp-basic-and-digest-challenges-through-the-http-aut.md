---
id: BL-314
title: Answer RTSP Basic and Digest challenges through the HTTP authentication session in Surl.Protocol.Rtsp
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-313]
touches: [Surl.Protocol.Rtsp.UnitLibrary, Surl.Protocol.Rtsp.UnitTests]
requirement: FR-052
created: 2026-09-30
completed: 2026-09-30
---
# BL-314 — Answer RTSP Basic and Digest challenges through the HTTP authentication session in Surl.Protocol.Rtsp

## Goal

`RtspProtocolServer` judges every request's `Authorization` field through `IHttpAuthenticationSession`
and answers `401` with one `WWW-Authenticate` field per challenge value, refusing `Basic` over
`rtsp://` without `--allow-plaintext-auth`, as BL-286's ADR and ADR-0032 decide.

## Context

- Decisions: BL-286's ADR (which requests need a login, anonymous reads, the `401`/`403` answers and
  their fields, the connection's persistence after a refusal); ADR-0032 section 6 (the session
  contract, the challenge values in order), criterion 3 (a clear password without TLS refused
  unchecked - RTSP has no TLS scheme in curl), ADR-0036 (Digest nonces), ADR-0038 (the login note).
- Code: `IHttpAuthenticationSession`, `HttpAuthenticationRequest`, `HttpAuthenticationVerdict` in
  `Surl.Protocol.Abstractions`; the challenge-field writing BL-293 moved into `Surl.HttpMessage`; the
  HTTP server's `HttpRequestResponder` as the pattern (a Digest `uri` here is an `rtsp://` URL).
- Fixtures: the `-u` cases of BL-286's ADR (`--basic`, `--digest`) recorded with the Windows reference
  build, both the first request and the one answering the challenge.

## Acceptance criteria

- [x] Tests in `Surl.Protocol.Rtsp.UnitTests` show, with a fake `IHttpAuthenticationSession`: a
      request with no credentials challenged as the ADR says; the recorded Digest answer accepted and
      the request served; a wrong answer refused; `Basic` over `rtsp://` refused unchecked, and
      accepted with `--allow-plaintext-auth`; the login note written to the exchange log; no test
      opens a socket.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Rtsp.UnitLibrary`.

## Notes

- Built ADR-0074 decision 7 as written; no new ADR was needed. `RtspProtocolServer` now takes an
  `IAuthenticationPolicy` (RTSP is not composed in `Surl.Console` yet - BL-317 does that - so the
  constructor change stays inside this task's touches) and starts one session per connection with
  `connection.TlsSession` (null for `rtsp://`). The policy, not the server, refuses Basic and
  Bearer unchecked over plain text without `--allow-plaintext-auth`, as for HTTP; the server's part
  is to start the session with no TLS and to answer `Forbidden` with `403`.
- The login is check 4: after `CSeq` and body framing, before the method (so `GET` is challenged,
  not `501`). `401` and `403` both carry the `CSeq` and keep the connection (decision 2 does not
  list them as closing); any body is read first, so the next request starts where curl expects.
- Choices taken: `IsWrite` is `ANNOUNCE`, `RECORD`, and a `SETUP` whose `Transport` has a `mode=`
  parameter naming `record` in any case or quoting (reading too many SETUPs as writes only asks for
  a login more often). The session's `WWW-Authenticate` values ride on every response to the
  judged request, as the HTTP server does (Negotiate's final token on a `200`). A verdict with a
  `BodyCheck` (ADR-0045) is honoured: the body is hashed with SHA-256 while it is discarded and the
  body check's verdict answers - cheap, and it keeps RTSP from silently ignoring a body-bound login.
- Fixtures `no-login-401`, `digest-login`, `basic-login`, `basic-forbidden-403` recorded 2026-09-30
  with the pinned win-x64 reference build (Fixtures/README.md); curl completed each as ADR-0074's
  measurements say (Digest's second leg on the same connection with `uri="*"`).
- Coverage: `Measure-CodeQuality.ps1 -Library Surl.Protocol.Rtsp.UnitLibrary` - 100% line, 100%
  branch, 0 failing members. 25 new tests (107 in the project).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. RTSP judges every request's login through IHttpAuthenticationSession: 401 with the session's challenges, 403, Digest answered on the same connection
