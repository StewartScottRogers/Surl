---
id: BL-778
title: Answer an MCP connection past the connection limits with 503
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-466, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-778 — Answer an MCP connection past the connection limits with 503

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

- 2026-11-11: `dotnet build` fails with "A compatible .NET SDK was not found": global.json pins 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Added BL-1035 to depends-on; no code written.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-11: Backlog -> Doing.
