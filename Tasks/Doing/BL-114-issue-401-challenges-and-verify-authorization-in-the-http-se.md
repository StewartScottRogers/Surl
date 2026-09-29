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
completed:
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

- [ ] Tests replay each recorded request through the server with a fake contract and pin the
      full response bytes: `401` with the contract's challenges when refused, the file's `200`
      when accepted, and ADR-0032's answer for credentials sent when no account exists.
- [ ] Tests prove an anonymous `GET` and `HEAD` are served or challenged exactly as
      ADR-0032 decision 4 says, and that the server passes the connection's encryption
      (`TlsSession` null or not) to the contract.
- [ ] A test proves a connection-bound handshake (a fake two-round method standing in for
      NTLM) keeps its state across two requests on one keep-alive connection and loses it
      on a new connection, as ADR-0032 decision 6 says.
- [ ] No `Authorization` value, password or token appears in any response byte or in the
      exchange log's notes (the bytes-received log still records the raw request, as today).
- [ ] `ProtocolIsolationTests` pass (no new project reference); `dotnet build
      Surl.Protocol.Http.UnitLibrary -warnaserror` and `dotnet build Surl.Console
      -warnaserror` are clean; the fast tests pass; 100% line and branch coverage kept.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
