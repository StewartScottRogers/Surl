---
id: BL-1398
title: Refuse a WebTransport session with no login when no account is configured and --allow-anonymous is off
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-19
completed:
---
# BL-1398 — Refuse a WebTransport session with no login when no account is configured and --allow-anonymous is off

## Goal

`surl --self-signed webtransport://127.0.0.1:0/` with no account and no `--allow-anonymous` answers an extended
`CONNECT` that carries no `authorization` with `:status 401` and opens no session, as it already does once an
account exists.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 5 (webtransport part); cookbook review
  `Z:\repos\Surly\Book\parts\05-new-protocols\chapters\03-remote-calls-and-streams\recipes\webtransport-open-a-stream\review.md`
  finding 5: with no account, sessions open for anyone; `surl --aihelp webtransport` says "without an account a
  login is refused unless `--allow-anonymous` is given".
- Judged code, not help: the cause is the same as gRPC's (BL-1397).
  `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary/WebTransportConnectAuthentication.cs:34` judges
  the `CONNECT` with `IsWrite: false`, and `Foundation/Surl.Authentication.UnitLibrary/HttpAuthenticationSession.cs:180`
  proceeds anonymously for a non-write when no account exists. That rule is ADR-0032 section 4's for HTTP reads;
  ADR-0032's Context items 1 and 2 limit anonymous access to HTTP, Gopher and TFTP, and ADR-0117 decision 7 is
  headed "Secure by default" with `--allow-anonymous` as what serves a `CONNECT` without `authorization`. A
  WebTransport session is a two-way echo, not a read. SignalR (BL-1091, `SignalRRequestAnswerer.cs:55`) and
  Socket.IO already refuse in this setup.
- The fix: judge the `CONNECT` with `IsWrite: true`. NTLM and Negotiate handling in the same method is unchanged.
- Tests: `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests/WebTransportConnectAuthenticationTests.cs:42`
  asserts `Assert.IsFalse(request.IsWrite)` and must expect `true`; `UnitTestAuthenticationPolicy` records the
  requests.
- Stand-in: no store, event or fault change. The `AiHelpExamples` entry (`AiHelpExamples.cs:362`) already passes
  `--allow-anonymous`, so a test that wants an open echo server keeps working.

## Acceptance criteria

- [ ] A failing test first: `WebTransportConnectAuthenticationTests` shows the `CONNECT` is judged with
      `IsWrite: true` (the test at line 42 updated).
- [ ] A `WebTransportConnectAuthenticationTests` test with a policy that has no account and no
      `--allow-anonymous` answers a `CONNECT` without `authorization` `:status 401` with the verdict's
      `www-authenticate` values, and no session is registered.
- [ ] A test under `--allow-anonymous` still answers `200` and opens the session.
- [ ] `dotnet build Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and the library stays at 100% line and branch
      coverage. No test needs `TestCategory=Integration`.

## Notes

- ADR-0117 decision 7's text still names `IsWrite: false`; BL-1409 aligns it. BL-1408 rewords
  `--aihelp webtransport` after this lands.

## Log

- 2026-11-19: Created.
