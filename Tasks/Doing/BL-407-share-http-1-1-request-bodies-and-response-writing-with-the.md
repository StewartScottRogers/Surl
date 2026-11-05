---
id: BL-407
title: Share HTTP/1.1 request bodies and response writing with the extended HTTP servers
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-401, BL-366]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Every existing test in `Surl.Protocol.Http.UnitTests` and `Surl.HttpMessage.UnitTests` passes, and none of
      its expected bytes is edited. Tests of moved types move with them.
- [ ] A test in the shared library's test project drives a minimal handler through an `InMemoryConnection` with
      no `Surl.Protocol.Http` reference. It covers: a `PUT` with a `Content-Length` body, a chunked `POST`, an
      `Expect: 100-continue` request, two requests on one kept-alive connection, and a `Connection: close`
      request. The bytes asserted are written from RFC 9112 sections 6, 7 and 9.
- [ ] `ProtocolIsolationTests` passes with the library's row as the ADR states it.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
