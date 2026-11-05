---
id: BL-709
title: Reset HTTP/3 streams carrying NTLM or Negotiate with H3_VERSION_FALLBACK
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-383, BL-602]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-709 — Reset HTTP/3 streams carrying NTLM or Negotiate with H3_VERSION_FALLBACK

## Goal

An HTTP/3 request whose `authorization` names NTLM or Negotiate is reset and stopped
`H3_VERSION_FALLBACK` (0x110), as ADR-0082 decision 9 says, the way BL-602 resets an HTTP/2 stream
`HTTP_1_1_REQUIRED`.

## Context

- ADR-0082 decision 9 ("Logins bound to a connection") and its Amendment 1, item 7; ADR-0081
  decision 7 for HTTP/2.
- `Http3RequestStreamServer` (BL-383) hands every request to `HttpRequestAnswerer`. Follow the seam
  BL-602 puts in the HTTP/2 connection, so the answerer still never asks which version carried a
  request.
- Tests drive the server with `Http3TestClient` in `Surl.Protocol.Http.UnitTests`.
- Filed by BL-383.

## Acceptance criteria

- [ ] A named test: a request stream whose `authorization` is `NTLM TlRM...` is reset and stopped
      `H3_VERSION_FALLBACK` (0x110), and the connection carries on.
- [ ] A named test: the same for `Negotiate`.
- [ ] A Basic login over HTTP/3 is answered as over HTTP/1.1.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http`
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
