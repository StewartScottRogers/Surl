---
id: BL-602
title: Reset HTTP/2 streams carrying NTLM or Negotiate with HTTP_1_1_REQUIRED
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-602 — Reset HTTP/2 streams carrying NTLM or Negotiate with HTTP_1_1_REQUIRED

## Goal

An HTTP/2 stream whose `authorization` names NTLM or Negotiate, or whose only acceptable challenge for the account would be one of them, is answered `RST_STREAM` `HTTP_1_1_REQUIRED`, as ADR-0081 decision 7 says.

## Context

- ADR-0081 decision 7 ("Logins bound to a connection"), with its measured `HEADERS` carrying `authorization: NTLM` and `Negotiate`; ADR-0090 decision 6.
- Today `Http2ConnectionServer` hands every stream to `HttpRequestAnswerer`, whose connection's authentication session would start an NTLM handshake across streams. The decision belongs in the HTTP/2 connection, not the answerer (the answerer never asks which version carried a request).
- Filed by BL-368.

## Acceptance criteria

- [ ] A named test: a stream whose `authorization` is `NTLM TlRM...` (ADR-0081's measured value) is reset `HTTP_1_1_REQUIRED` and the connection carries on.
- [ ] A named test: the same for `Negotiate`, and for a request whose account's only offered challenge is NTLM or Negotiate.
- [ ] A Basic or Digest login over HTTP/2 is answered as over HTTP/1.1.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
