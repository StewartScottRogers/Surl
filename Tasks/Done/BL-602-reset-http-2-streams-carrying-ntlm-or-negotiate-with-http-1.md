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
completed: 2026-11-05
---
# BL-602 — Reset HTTP/2 streams carrying NTLM or Negotiate with HTTP_1_1_REQUIRED

## Goal

An HTTP/2 stream whose `authorization` names NTLM or Negotiate, or whose only acceptable challenge for the account would be one of them, is answered `RST_STREAM` `HTTP_1_1_REQUIRED`, as ADR-0081 decision 7 says.

## Context

- ADR-0081 decision 7 ("Logins bound to a connection"), with its measured `HEADERS` carrying `authorization: NTLM` and `Negotiate`; ADR-0090 decision 6.
- Today `Http2ConnectionServer` hands every stream to `HttpRequestAnswerer`, whose connection's authentication session would start an NTLM handshake across streams. The decision belongs in the HTTP/2 connection, not the answerer (the answerer never asks which version carried a request).
- Filed by BL-368.

## Acceptance criteria

- [x] A named test: a stream whose `authorization` is `NTLM TlRM...` (ADR-0081's measured value) is reset `HTTP_1_1_REQUIRED` and the connection carries on.
- [x] A named test: the same for `Negotiate`, and for a request whose account's only offered challenge is NTLM or Negotiate.
- [x] A Basic or Digest login over HTTP/2 is answered as over HTTP/1.1.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps 100% line and branch coverage.

## Notes

- Where: `Http2StreamAnswerWriter` (this library's `IHttp2StreamHandler`), not `Surl.Http2`'s `Http2ConnectionServer`. It is the HTTP/2 side of the connection and can reset its stream through `Http2RequestStream.ResetAsync`, and it sits inside this task's `touches`. The answerer is still never told which version carried a request.
- The `authorization` check runs before the answerer, so the connection's authentication session never sees the NTLM or Negotiate credentials and no handshake starts. The auth-scheme matches case-insensitively (RFC 9110 section 11.1).
- "The account's only acceptable challenge is NTLM or Negotiate" means: the answer is a `401` with at least one `WWW-Authenticate` value, and every value's scheme is NTLM or Negotiate. A `401` that also offers Basic, Digest or anything else is answered as over HTTP/1.1. So is a `401` with no challenge, and so is any non-401 answer.
- Test values: `NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==` is the type-1 message this repository's NTLM tests already use (ADR-0081 records it only as `NTLM TlRM...`). Only the scheme token decides the outcome, so the token bytes do not matter.
- Tests in `Http2ConnectionServerTests`: `ServeAsync_StreamWhoseAuthorizationIsNtlmOrNegotiate_IsResetHttp11RequiredUnjudgedAndTheConnectionCarriesOn`, `ServeAsync_RequestWhoseOnlyChallengesAreNtlmOrNegotiate_IsResetHttp11RequiredAndTheConnectionCarriesOn`, `ServeAsync_ChallengeOfferingALoginBoundToTheRequest_IsAnswered401AsOverHttp11`, `ServeAsync_BasicOrDigestLogin_IsAnswered200AsOverHttp11`. Results: 471 passed in `Surl.Protocol.Http.UnitTests`, and `Surl.Protocol.Http.UnitLibrary` has 100% line and branch coverage (cobertura).
- Follow-up BL-826: `AiHelpProse.cs` (the `http` topic) and ADR-0090 decision 6 still say this reset is not built. Neither could be edited here, because `Application/Surl.Cli.UnitLibrary` is held by BL-653 and `Documentation/Planning/Decisions` by BL-569. The `H3_VERSION_FALLBACK` reset for HTTP/3 is BL-709, which already exists.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. HTTP/2 streams carrying NTLM or Negotiate, or challenged only for them, are reset HTTP_1_1_REQUIRED and the connection carries on
