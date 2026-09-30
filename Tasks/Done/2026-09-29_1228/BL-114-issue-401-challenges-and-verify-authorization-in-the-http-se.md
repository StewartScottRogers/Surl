---
id: BL-114
title: Issue 401 challenges and verify Authorization in the HTTP server through the authentication contract
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-109]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-114 — Issue 401 challenges and verify Authorization in the HTTP server through the authentication contract

## Goal

The HTTP server asks BL-109's authentication contract, for every request, whether a login
is needed and whether the `Authorization` it carries is accepted, and answers `401` with the
contract's `WWW-Authenticate` lines when it is not - so `http` and `https` enforce ADR-0032
decision 4 without referencing `Surl.Authentication`.

## Context

FR-014; ADR-0032 (BL-100) decision 4 (which requests need a login, the `401`'s status line,
headers and body, what a request with credentials gets when no account exists, what Basic
over `http://` gets) and decision 6 (the contract, including connection-bound handshake state
for NTLM and Negotiate). BL-109 added the contract.

- `Surl.Protocol.Http.UnitLibrary/HttpProtocolServer.cs`: constructor
  `HttpProtocolServer(ContentStore)`; add the contract as a constructor parameter (keep the
  existing constructor only if ADR-0032 says an HTTP server with no authentication is still
  composed anywhere; `Surl.Console` changes in BL-117, so keep `Surl.Console` compiling with
  an overload or a default the ADR allows).
- `HttpRequestResponder.cs` answers `GET`/`HEAD` and refuses others (`405`);
  `HttpResponseHead.cs`, `HttpStatus.cs` build responses; `HttpRequestHead.cs`/
  `HttpRequestField.cs` give the fields; `connection.TlsSession` (ADR-0010) says whether the
  connection is encrypted.
- Tests: `Surl.Protocol.Http.UnitTests` with `HttpServerHarness.cs`, `RecordedFixture.cs`
  and fixtures under `Fixtures/` (see its `README.md`); write a fake of the contract in the
  test project (no mocking library).
- Measure what upstream curl sends to, and prints for, the decided `401`: with
  `Record-CurlExchange.ps1` and the pinned reference build (curl 8.21.0), `-Connections 2`,
  record `-sS -u tester:secret --basic`, `-sS --digest -u tester:secret`,
  `-sS --oauth2-bearer tok`, and `-sS -f` with no credentials (exit 22 expected, measure it);
  save each as a fixture beside the existing ones.

## Acceptance criteria

- [x] Tests replay each recorded request through the server with a fake contract and pin the
      full response bytes: `401` with the contract's challenges when refused, the file's `200`
      when accepted, and ADR-0032's answer for credentials sent when no account exists.
- [x] Tests prove an anonymous `GET` and `HEAD` are served or challenged exactly as
      ADR-0032 decision 4 says, and that the server passes the connection's encryption
      (`TlsSession` null or not) to the contract.
- [x] A test proves a connection-bound handshake (a fake two-round method standing in for
      NTLM) keeps its state across two requests on one keep-alive connection and loses it
      on a new connection, as ADR-0032 decision 6 says.
- [x] No `Authorization` value, password or token appears in any response byte or in the
      exchange log's notes (the bytes-received log still records the raw request, as today).
- [x] `ProtocolIsolationTests` pass (no new project reference); `dotnet build
      Surl.Protocol.Http.UnitLibrary -warnaserror` and `dotnet build Surl.Console
      -warnaserror` are clean; the fast tests pass; 100% line and branch coverage kept.

## Notes

- Plan: `HttpProtocolServer(ContentStore, IAuthenticationPolicy)`; the old one-argument
  constructor stays and passes `AnonymousAuthenticationPolicy`, as ADR-0032 section 6 says
  until BL-117, so `Surl.Console` compiles unchanged. `ServeAsync` calls
  `StartHttpConnection(connection.TlsSession)` once per connection before the first head.
- Judged in `HttpRequestResponder.AnswerRequestAsync` after the `Host` check and the invalid
  `Content-Length` 400 (both are "not well-formed", ADR-0032 section 4) and before the 413,
  `100 Continue`, any body read and method dispatch. `Challenge` -> `401 Unauthorized`, kept
  alive as a 404 is, but a refusal (`Connection: close`, half-close, drain) when the request
  announced a body; `Forbidden` -> `403 Forbidden` as a refusal; `Proceed` -> served, and its
  values are written on that response.
- Decision (sensible default, within ADR-0032): `WWW-Authenticate` fields go after
  `Content-Length` and before `Connection`, the order ADR-0032 section 4 lists them in; the
  values the contract gives are written on every response to the judged request, 403
  included, since the server "decides nothing about methods".
- Measured with pinned curl 8.21.0: fixtures `basic-401`, `digest-401-then-200`,
  `bearer-401`, `anonymous-401-fail` (exit 22), and `basic-plaintext-403` (exit 22, added for
  ADR-0032's Basic-over-`http://` answer); commands in `Fixtures/README.md`. curl's Digest
  answered the MD5 line on a new connection.
- Log notes name only method and target; a test checks that no response byte or note holds
  the Basic credentials, the bearer token, the Digest response or the NTLM stand-in tokens.
- Fakes: `UnitTestAuthenticationPolicy` (a verdict function, recording TLS sessions and
  requests) and `UnitTestTwoRoundAuthenticationPolicy` (NTLM stand-in, state per session).
- Coverage: `Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary` 100% line, 100%
  branch, 0 failing members (worst CRAP 10). HTTP tests 341, all fast tests green.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. HTTP server asks the authentication contract per request: 401 with its challenges, 403, or served; handshake state bound to the connection
