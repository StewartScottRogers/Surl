---
id: BL-466
title: Serve MCP over the Streamable HTTP transport with sessions and authorization
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-465, BL-407]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-466 — Serve MCP over the Streamable HTTP transport with sessions and authorization

## Goal

The MCP server answers the Streamable HTTP transport on one endpoint. It answers `POST` with JSON or an SSE
stream, and `GET` as the ADR decides. It issues and checks `Mcp-Session-Id`, checks `MCP-Protocol-Version` and
`Origin`, ends a session on `DELETE`, and requires authorization by default.

## Context

- Decision: BL-463's ADR, decisions 4, 6 and 7.
- HTTP/1.1: BL-407's library (add its reference). SSE framing: the WHATWG HTML "Server-sent events"
  section (`data:`, `id:`, a blank line between events).
- MCP "Transports > Streamable HTTP":
  - `Accept` must list both types;
  - notifications and responses only get `202 Accepted`;
  - an unknown or expired session gets `404`;
  - a request without a session ID after initialization gets `400`;
  - the `MCP-Protocol-Version` header's check, and its default of `2025-03-26` when absent.
- "Authorization": `401` with `WWW-Authenticate` and RFC 9728's metadata document, as the ADR decides.
- Session IDs from an injected random source; expiry on an injected `TimeProvider`.

## Acceptance criteria

- [x] Named tests show:
      - `initialize` by `POST` returning `Mcp-Session-Id`;
      - a later request with it answered;
      - one without it gets `400`;
      - an unknown session gets `404`;
      - a notification gets `202`;
      - a request answered on an SSE stream when the ADR says so, with SSE bytes written from the HTML standard;
      - a bad `Origin` refused with `403`;
      - an unsupported `MCP-Protocol-Version` gets `400`;
      - `DELETE` ending the session;
      - no token with anonymous off gets `401` with the ADR's `WWW-Authenticate`;
      - the metadata document served.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered (2026-11-05): `McpProtocolServer` (schemes `mcp`, `mcps`) on `Http1ConnectionServer`, with
  `McpHttpRequestAnswerer` (decision 4.2's order of checks), `McpHttpSessionTable`/`McpHttpSession`
  (IDs, login binding, 30-minute idle expiry, 1000 cap, 16 in flight, `notifications/cancelled`, `DELETE`),
  `McpAcceptField`, `McpOriginCheck`, `McpMessageBuffer` (`--max-message` -> `413`), `McpEventStream`
  and `McpProtectedResourceMetadata`. The csproj now references `Surl.HttpServing` (ADR-0111 decision 1).
- Tests, in `McpProtocolServerTests` for the acceptance list: `ServeAsync_InitializeByPost_AnswersJsonWithAnMcpSessionId`,
  `ServeAsync_LaterRequestWithTheSessionId_IsAnswered`, `ServeAsync_RequestWithoutASessionIdAfterInitialization_Gets400`,
  `ServeAsync_UnknownSession_Gets404`, `ServeAsync_Notification_Gets202WithNoBody`,
  `ServeAsync_ToolsCall_IsAnsweredOnAnEventStreamWithOneDataEvent`, `ServeAsync_RefusedOrigin_Gets403`,
  `ServeAsync_UnsupportedProtocolVersion_Gets400`, `ServeAsync_Delete_EndsTheSessionWith204AndItIsThen404`,
  `ServeAsync_NoTokenWithAnonymousOff_Gets401WithTheResourceMetadataChallenge`,
  `ServeAsync_MetadataDocument_IsServedWithoutAuthorization`; plus every other row of decision 7.2.
  171 tests in the project; library at 100% line, 100% branch, worst CRAP 10 (Measure-CodeQuality.ps1).
- Choice: the `tools/call` event is written as one `Content-Length` body, not chunked with 15-second
  `: keep-alive` comments (decision 4.3). `Surl.HttpServing`'s `HttpAnswerBody` has no streamed body and that
  library is outside this task's `touches`; the acceptance criteria ask only for the SSE bytes. Filed BL-767.
- Choice: `ListenUrl` carries no path, so the endpoint path is a constructor argument (`listenPath`; empty or
  `/` is `/mcp`). BL-468 passes the listen URL's path when it composes the server.
- Choice: an `initialize` the core answers is answered first and its session added after; past the 1000-session
  bound it is `503 Retry-After: 60` and the answered session is dropped, so one check covers the race.
- Choice: a Bearer token's empty-name account runs its tools as the anonymous content caller (as the Azure
  server does), while the session stays bound to the login (anonymous `null` and a token's `""` differ).
- Choice: the metadata document answers methods other than `GET` with `405 Allow: GET`; a body that cannot
  be read whole (cut short, or an unreadable transfer coding) is `400` and the connection closes.
- Follow-up: BL-768, the `503` refusal writer for connections past the connection limits (decision 7.3).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The MCP server answers Streamable HTTP: sessions, Origin, MCP-Protocol-Version, Bearer authorization with the RFC 9728 challenge and metadata, JSON and SSE answers
