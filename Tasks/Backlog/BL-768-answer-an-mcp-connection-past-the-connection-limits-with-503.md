---
id: BL-768
title: Answer an MCP connection past the connection limits with 503
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-466]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-768 — Answer an MCP connection past the connection limits with 503

## Goal

`McpProtocolServer` implements `IConnectionRefusalWriter`, so a connection past `--max-connections` or
`--max-connections-per-address` is answered `503 Service Unavailable` with no body and closed, as ADR-0111
decision 7.3's limits table says.

## Context

- BL-466 built `McpProtocolServer` without a refusal writer.
- Model: `AzureStorageProtocolServer.WriteRefusalAsync` (`Date`, `Server: surl`, `Content-Length: 0`,
  `Connection: close`, then `CompleteWritesAsync`), with this server's empty body.

## Acceptance criteria

- [ ] A named test shows `WriteRefusalAsync` writes `HTTP/1.1 503 Service Unavailable` with `Date`, `Server: surl`, `Content-Length: 0` and `Connection: close`, and completes writes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
