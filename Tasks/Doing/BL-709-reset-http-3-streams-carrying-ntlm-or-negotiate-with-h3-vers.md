---
id: BL-709
title: Reset HTTP/3 streams carrying NTLM or Negotiate with H3_VERSION_FALLBACK
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-383, BL-602]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary, Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md]
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
- `Http3StreamAnswerWriter` (BL-383, made an `IHttp3RequestStreamHandler` by BL-410) hands every request to `HttpRequestAnswerer`. Follow the seam
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

- Added `Protocols/Http/Surl.Http3.UnitLibrary` to `touches`: `Http3ErrorCode` had no
  `H3_VERSION_FALLBACK` (0x110), so it gained `VersionFallback`. No task in Doing names that
  project. Also added ADR-0082, whose Amendment 1 item 7 said this was not built yet; it now
  says BL-709 built it.
- The NTLM and Negotiate checks moved out of `Http2StreamAnswerWriter` into one internal
  `ConnectionBoundLogins` both writers use, so the answerer still never asks the version.
- Decided (mirrors BL-602 and ADR-0082 decision 9): a `401` whose every challenge is NTLM or
  Negotiate is also reset and stopped `H3_VERSION_FALLBACK`, as HTTP/2 resets it
  `HTTP_1_1_REQUIRED`; a `401` offering any request-bound login is answered as over HTTP/1.1.
- Tests in `Http3ConnectionServerTests.Logins.cs`: NTLM, Negotiate, only-NTLM/Negotiate
  challenges, a `401` offering Basic, and a Basic login answered `200`. Whether upstream curl
  then retries over TCP stays BL-385's measurement.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
