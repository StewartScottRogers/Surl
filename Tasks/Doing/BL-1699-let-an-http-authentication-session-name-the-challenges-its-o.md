---
id: BL-1699
title: Let an HTTP authentication session name the challenges its own 401 offers
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1699 — Let an HTTP authentication session name the challenges its own 401 offers

## Goal

`IHttpAuthenticationSession` has a `CreateChallenges()` member returning the `WWW-Authenticate` values the
connection's own `401` would offer now, so a server answering a `401` it did not get from the session (BL-1700) can
still send a challenge.

## Context

- Source: Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-25T1430-Surl-bug-report.md`, item 3
  (Low), against Surl v0.1.8 `9f8005dff`. Repro, copied from the report: Surl.Api, an `https://` server with
  accounts and an override that returns `SurlFault.ProtocolError("401")` for a path (recipe
  `http-expire-tokens-with-a-response-override`, case `answer`); `curl -D - https://127.0.0.1:P/<path>` shows
  `HTTP/1.1 401` with no `WWW-Authenticate` line. Expected: at least one challenge, such as the server's own
  `Bearer realm=...`. RFC 9110 section 11.6.1: a server generating a 401 MUST send a `WWW-Authenticate` field
  with at least one challenge.
- Why: ADR-0134 decision 1 has the chosen error carry "any challenges" - the verdict's `WwwAuthenticateValues` -
  but a request let in has a `Proceed` verdict, which carries none. Nothing on the contract lets the HTTP server
  ask for a challenge without judging a request. This task adds that; BL-1700 uses it.
- Design (Claude under Stewart's delegation; BL-1700 records it in ADR-0134):
  - `Foundation/Surl.Protocol.Abstractions.UnitLibrary/IHttpAuthenticationSession.cs` gains
    `IReadOnlyList<string> CreateChallenges() => [];` - a default interface member, so the many test fakes of the
    interface across the solution keep compiling. Its doc comment says what it returns and that it judges nothing.
  - `Foundation/Surl.Authentication.UnitLibrary/HttpAuthenticationSession.cs` implements it with the same list
    its private `Challenge()` builds (the policy's HTTP methods, without plaintext-secret methods unless
    `OffersPlaintextSecrets` for this connection), and `Challenge()` reuses it. An empty list means the session
    offers no method on this connection.
  - Protocol servers are not touched here.
- Stand-in: no store, no event; a pure contract addition.

## Acceptance criteria

- [ ] A named test in `Foundation/Surl.Protocol.Abstractions.UnitTests` shows an implementation that does not
      override `CreateChallenges` returns an empty list.
- [ ] Named tests in `Foundation/Surl.Authentication.UnitTests/HttpAuthenticationSessionTests.cs`: with Basic,
      Digest and Bearer offered over TLS, `CreateChallenges()` returns the same values, in the same order, as the
      `Challenge` verdict for a request without `Authorization`; over plain HTTP without `--allow-plaintext-auth`
      it omits Basic as that verdict does; with no HTTP method it is empty.
- [ ] Regression test for this report's root cause: a session that just returned `Proceed` for a valid Bearer
      login still returns `Bearer realm="surl"` from `CreateChallenges()`.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` and
      `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` are clean, the fast tests of the
      whole solution are green, and both libraries stay at 100% line and branch coverage.

## Notes

- Four projects because the contract member is a few lines and its only implementation is in Surl.Authentication;
  the HTTP server's use of it is BL-1700.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
