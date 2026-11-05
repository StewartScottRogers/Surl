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
completed:
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

- [ ] Every existing test in `Surl.Protocol.Http.UnitTests` passes, and none of its expected bytes is
      edited.
- [ ] The answering type depends on no HTTP/1.x framing type. A test drives it with no `IConnection`
      and asserts the decided status, fields and body for a `GET` of a content-store file, a `HEAD`, a
      `401` with its challenges, and a `404`.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~OverHttp"` passes on Windows.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
