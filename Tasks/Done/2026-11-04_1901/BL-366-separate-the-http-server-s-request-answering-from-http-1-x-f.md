---
id: BL-366
title: Separate the HTTP server's request answering from HTTP/1.x framing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-364]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-366 — Separate the HTTP server's request answering from HTTP/1.x framing

## Goal

The HTTP server works out its answer to a request (status, fields, body source, and whether the
connection may carry on) without writing HTTP/1.x bytes itself. An HTTP/1.x writer then turns that
answer into exactly the bytes sent today. BL-368 and BL-383 can then serve the same answers over
HTTP/2 and HTTP/3 streams.

## Context

- Decision: BL-364's ADR, decision 8 (the shape and names). If it names the types, use its names.
- `Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestResponder.cs` (446 lines) takes an `IConnection`,
  `HttpConnectionReader`, `ExchangeContext`, `ContentStore` and `IHttpAuthenticationSession`.
  `AnswerRequestAsync(HttpRequestHead)` both decides the answer and writes it. The related types are
  `ConnectionWriteStream`, `HttpConnectionPersistence`, `HttpRequestBodyDiscarder` and
  `HttpUnreadRequestDrainer`. Response heads are `HttpResponseHead` in `Surl.HttpMessage`
  (ADR-0070).
- HTTP/1.x-only behaviour stays on the HTTP/1.x side: `100 Continue` (ADR-0027), drain before close
  (ADR-0024), the lingering close (ADR-0021), `Connection: close` and keep-alive, and chunked
  framing. The decision side must not ask which HTTP version it is answering.
- This is a refactor. Bytes on the wire must not change.

## Acceptance criteria

- [x] Every existing test in `Surl.Protocol.Http.UnitTests` passes, and none of its expected bytes is
      edited.
- [x] The answering type depends on no HTTP/1.x framing type. A test drives it with no `IConnection`
      and asserts the decided status, fields and body for a `GET` of a content-store file, a `HEAD`, a
      `401` with its challenges, and a `404`.
- [x] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~OverHttp"` passes on Windows.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps
      100% line and branch coverage.

## Notes

- Delivered directly in the session (a refactor against ADR-0081 decision 8's names) rather than
  through the full `/feature` agent chain: the plan is the ADR, and the gate is the unchanged tests.
- Shape, using ADR-0081's names: `HttpRequestAnswerer` (decides; takes `ExchangeContext`,
  `ContentStore`, `IHttpAuthenticationSession`, no `IConnection`), `HttpRequest`, `IHttpRequestBody`,
  `HttpAnswer` (status, fields, `HttpAnswerBody`, `IsRefusal`, `LeavesRequestBodyUnread`),
  `HttpAnswerBody` (`None`, `Bytes`, `ContentFile`), and `Http1AnswerWriter`.
  `HttpRequestResponder` became `Http1RequestResponder`, the HTTP/1.x coordinator.
- Choices beyond the ADR's text, with why:
  - `100 Continue` lives in `Http1RequestBody.DiscardAsync`, not in the writer: it must be written
    *during* the answerer's body read, before the answer exists. It is still HTTP/1.x-side.
  - The `Host` check and the invalid `Content-Length` refusal stay HTTP/1.x-side
    (`Http1RequestResponder`): both are RFC 9112 rules. They use the answerer's public
    `Refuse(status, note)` so the refusal's fields and log note are built in one place.
  - `IsRefusal` stands for "the server reads no more requests after this one"; the HTTP/1.x writer
    maps it to `Connection: close`, the one-second write deadline, half-close and drain.
  - The answer's fields are everything but `Connection`; the HTTP/1.x writer appends it last, which
    is where it was before, so every byte is unchanged.
  - `HttpAnswerBody.Bytes` is unused by the answerer today (the ADR names it); its write is pinned by
    `Http1AnswerWriterTests` so coverage stays 100%.
- Verified: 206/206 `Surl.Protocol.Http.UnitTests` (199 untouched + 7 new), library at 100% line and
  branch; OverHttp conformance 39 passed, 2 skipped (Linux/macOS-only by `OSCondition`); full
  `dotnet build -warnaserror` clean; fast tests green in all 39 test projects.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. HttpRequestAnswerer decides each HTTP answer with no connection or version; Http1AnswerWriter writes the same bytes as before
