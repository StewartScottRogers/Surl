---
id: BL-407
title: Share HTTP/1.1 request bodies and response writing with the extended HTTP servers
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-401, BL-366]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests, Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests, Documentation/Wiki/Glossary.md, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-407 — Share HTTP/1.1 request bodies and response writing with the extended HTTP servers

## Goal

Several extended servers answer HTTP/1.1 requests: S3, AzureStorage, GoogleCloudStorage, OciRegistry,
McpServer, the SignalR negotiate request and DNS over HTTPS. After this task they can do so through a horizontal
library, without referencing `Surl.Protocol.Http`. That library reads a request body (`Content-Length` or
chunked, with `100 Continue`), writes a response with a body, and keeps or closes the connection. The HTTP
server's bytes do not change.

## Context

- Decision: BL-401's ADR, the HTTP/1.1 section. Use its library name, the types it says move, and its
  request-handler shape. If it creates a new library rather than extending `Surl.HttpMessage`, create it beside
  `Surl.HttpMessage` in `Protocols/Http/` with its `.UnitTests` twin, add both to `Surl.slnx` in the
  `/Protocols/Http/` solution folder, add its row to `ProtocolIsolationTests`, and correct this task's `touches`.
- Starting point: after BL-366, `Protocols/Http/Surl.Protocol.Http.UnitLibrary` keeps HTTP/1.x framing in its
  own types (`ConnectionWriteStream`, `HttpConnectionPersistence`, `HttpRequestBodyDiscarder`,
  `HttpUnreadRequestDrainer`, the chunked reader, the lingering close of ADR-0021). ADR-0070 is the model for
  moving types between libraries: names unchanged, `public` only what a server uses, `InternalsVisibleTo` the
  library's own tests.
- The HTTP-specific behaviour (content store, authentication judging, ranges) stays in `Surl.Protocol.Http`.

## Acceptance criteria

- [x] Every existing test in `Surl.Protocol.Http.UnitTests` and `Surl.HttpMessage.UnitTests` passes, and none of
      its expected bytes is edited. Tests of moved types move with them.
- [x] A test in the shared library's test project drives a minimal handler through an `InMemoryConnection` with
      no `Surl.Protocol.Http` reference. It covers: a `PUT` with a `Content-Length` body, a chunked `POST`, an
      `Expect: 100-continue` request, two requests on one kept-alive connection, and a `Connection: close`
      request. The bytes asserted are written from RFC 9112 sections 6, 7 and 9.
- [x] `ProtocolIsolationTests` passes with the library's row as the ADR states it.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- ADR-0084 decision 1 creates a new library, so this task built `Protocols/Http/Surl.HttpServing.UnitLibrary`
  and `.UnitTests` (namespace `Surl.HttpServing`), listed both under `/Protocols/Http/` in `Surl.slnx`, and added
  `[HttpServing] = [Abstractions, HttpMessage, Content]` to `ProtocolIsolationTests.HorizontalLibraries`.
  `touches` gained the two new projects and `Documentation/Wiki/Glossary.md` (ADR-0084's consequences ask for the
  takeover's glossary entry); no task in Doing names any of them.
- Moved, names unchanged: `HttpRequest`, `IHttpRequestBody`, `HttpRequestBodyDiscardOutcome`, `HttpAnswer`,
  `HttpAnswerBody` (public); `Http1RequestBody`, `Http1RequestResponder`, `Http1AnswerWriter`,
  `ConnectionWriteStream`, `HttpConnectionPersistence`, `HttpRequestBodyDiscarder`, `HttpUnreadRequestDrainer`
  (internal). New: `IHttpRequestHandler`, `Http1ConnectionServer`, `IHttpConnectionTakeover` (public).
  `HttpRequestAnswerer` implements `IHttpRequestHandler`; `HttpProtocolServer.ServeAsync` now starts the
  authentication session, builds the answerer and awaits `Http1ConnectionServer.ServeAsync`.
- Choices made under the delegation (sensible defaults within ADR-0084, recorded here because BL-550 holds
  `Documentation/Planning/Decisions`):
  - `HttpAnswerBody.ContentFile` carries its `ContentStore`, so the shared writer needs no store of its own and
    an object-store server answers files from whichever store it maps.
  - The malformed-head refusals (`400`, `408`, `431`, `505`) are built by `Http1RequestResponder` itself with
    the same fields and log note `HttpRequestAnswerer.Refuse` gave, so the handler never sees them; the
    answerer's public `Refuse(status, note)` went, unused.
  - `IHttpRequestBody` gained `CopyToAsync(Stream, CancellationToken)`, through the discarder's new optional
    copy stream: the goal says the library *reads* a body, and an upload server must keep it, not only hash it.
  - The takeover is built now (ADR-0084 decision 1 assigns it to BL-407): an answer with `Takeover` has its head
    written with no `Connection` field of the writer's own, then the takeover gets the connection and reader.
- Tests: `ConnectionWriteStreamTests`, `Http1AnswerWriterTests` and `HttpConnectionPersistenceTests` moved to
  `Surl.HttpServing.UnitTests`. `HttpUnreadRequestDrainerTests` drives `HttpProtocolServer` with recorded
  fixtures, so it stayed and is renamed `LingeringCloseTests`; it and `UploadLimitTests` use the harness's own
  `LingeringCloseDrainBytes`/`LingeringCloseDrainTime`/`MaxChunkLineBytes` now the constants are internal to
  another assembly. No expected byte was edited. `Http1ConnectionServerTests` adds the RFC 9112 cases.
- Measured: `Measure-CodeQuality.ps1 -Library Surl.HttpServing.UnitLibrary,Surl.Protocol.Http.UnitLibrary`
  reports 100% line and branch for both, worst CRAP 8 and 10.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.HttpServing serves HTTP/1.1 for any IHttpRequestHandler (bodies, 100 Continue, keep-alive, close, takeover); the HTTP server uses it with unchanged bytes
