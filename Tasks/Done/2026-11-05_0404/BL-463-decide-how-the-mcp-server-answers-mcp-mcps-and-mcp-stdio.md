---
id: BL-463
title: Decide how the MCP server answers mcp, mcps and mcp+stdio
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-612]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/McpServer.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-463 — Decide how the MCP server answers mcp, mcps and mcp+stdio

## Goal

An ADR decides how `surl mcp://`, `mcps://` and `mcp+stdio:` answer as a Model Context Protocol server (revision
2025-06-18). It covers the lifecycle and version negotiation, the tools served, pagination, both transports,
sessions, authorization and limits, and confirms the project name `Surl.Extended.Protocol.McpServer`. A
server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specification: MCP 2025-06-18
  (https://modelcontextprotocol.io/specification/2025-06-18):
  - "Base Protocol > Overview" (JSON-RPC 2.0; no batching in this revision);
  - "Lifecycle" (`initialize`, `protocolVersion` negotiation, capabilities, `notifications/initialized`,
    shutdown);
  - "Transports" (stdio, and Streamable HTTP with one endpoint for `POST` and `GET`, `Accept` of
    `application/json` and `text/event-stream`, `Mcp-Session-Id`, the `MCP-Protocol-Version` header, `DELETE`
    to end a session, and "Servers MUST validate the `Origin` header");
  - "Authorization" (OAuth 2.1, RFC 9728 protected resource metadata);
  - "Server Features > Tools" (`tools/list`, `tools/call`, `isError`, `notifications/tools/list_changed`);
  - "Utilities" (`ping`, pagination with `cursor`/`nextCursor`, cancellation).
- Which requests a client sends (what to accept, not a verdict): the port's
  `Curl.Extended.Protocol.McpClient.UnitLibrary` and its matrices `McpClient.md` and `McpClient-Stdio.md`. The
  port's reference server in `Curl.Conformance.Extended.UnitLibrary/McpClient/` is copyable code.
- HTTP/1.1 and SSE: BL-407's library. JSON: `Utf8JsonReader`/`Utf8JsonWriter` or a source-generated
  context only (native AOT).

- Shared MCP core (amended 2026-11-04): BL-612's ADR decides `Surl.McpServing`, the MCP core this server shares with
  the `Surl.Mcp` host - JSON-RPC 2.0 messages and error codes, the lifecycle with version negotiation, the tool and
  resource registry with pagination, and the newline-delimited stdio session - built by BL-619, BL-620, BL-621 and
  BL-623. This ADR adopts that ADR's protocol versions, unsupported-version answer, JSON-RPC error codes and
  message limit for decisions 3 and 7 rather than deciding them again, and decides only what is this server's own:
  its project, its tools, Streamable HTTP, how `mcp+stdio` fits the command line, authorization and its limits.

Decide and record:

1. The project name. Planning: `Surl.Extended.Protocol.McpServer`, in `Protocols/Http/`, named for what it is.
2. The tools served. Planning assumption: read-only tools over the content store (list a directory, read a
   file), and an echo tool, with `inputSchema` for each. Writes only under `--allow-uploads`.
3. Version negotiation: the versions accepted, and the answer for an unsupported one.
4. Streamable HTTP:
   - the endpoint path;
   - when a `POST` is answered with `application/json` and when with an SSE stream;
   - whether `GET` opens a server-to-client stream (or `405`);
   - session IDs (random, from an injected source) and their expiry;
   - `404` for an unknown session;
   - the `Origin` check and the allowed origins;
   - SSE event IDs and resumption (`Last-Event-ID`).
5. stdio: how `mcp+stdio` fits `surl [options] <url>` (surl reads JSON-RPC on its own standard input and writes
   to its standard output, logging only to standard error). Also whether it may be combined with other listen URLs.
6. Authorization: what secure-by-default means here. Planning assumption: bearer tokens from the accounts file
   checked on every HTTP request, with `401` and `WWW-Authenticate` naming RFC 9728 metadata that surl serves.
   stdio is trusted as the spec says ("Implementations using an STDIO transport SHOULD NOT follow this
   specification, and instead retrieve credentials from the environment"). Anonymous only under
   `--allow-anonymous`.
7. JSON-RPC error codes for each failure, and each ADR-0006 limit (the largest message, sessions).

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing the specification page behind
      it, and is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/McpServer.md` exists in the matrix format with every requirement of the
      pages above that binds a server, each "covered by" naming BL-464, BL-465, BL-466 or
      BL-467, or the core's BL-619, BL-620, BL-621 or BL-623, or "n/a: <reason>".

## Notes

- Decided in ADR-0111 (next free number at the time; a lane racing for 0111 would be caught at integration).
  Written directly in the session rather than through `align-and-document`, since it is a decision ADR plus its
  matrix and no `.cs` file changes, so `verify` does not apply; build and fast tests were still run.
- What the port's client sends was read for acceptance only: no `Authorization` and no `Origin`, so the server
  accepts a missing `Origin` (decision 4.7) and BL-469 must run with `--allow-anonymous`.
- The Authorization page requires `authorization_servers` to name at least one server, so the MCP server is its own
  OAuth 2.1 authorization server: filed as BL-722 (depends on BL-466), rather than widening this task.
- Defaults taken: endpoint `/mcp`; SSE only for `tools/call`; `GET` `405`; 30-minute idle sessions, 1000 a server;
  `mcp+stdio:` must be the only listen URL; no new option. Reasons are in the ADR.
- `Documentation/Product/Requirements.md` is not in `touches` and gains no row here, as with BL-429 and the
  other extended-server ADR tasks.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended by task-planner: depends on BL-612, whose ADR decides the MCP core (`Surl.McpServing`) this
  server shares with the `Surl.Mcp` host; decisions 3 and 7 adopt that core's versions and error codes.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0111 decides how the MCP server answers mcp, mcps and mcp+stdio, with its matrix McpServer.md; BL-722 filed for the authorization server
