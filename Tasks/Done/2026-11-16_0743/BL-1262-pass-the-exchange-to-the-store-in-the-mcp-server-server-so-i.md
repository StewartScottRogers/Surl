---
id: BL-1262
title: Pass the exchange to the store in the MCP server server so its writes match listener and scheme out-of-space rules and publish their exchange (ADR-0168)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1239, BL-1249]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: FR-062
created: 2026-11-16
completed: 2026-11-16
---
# BL-1262 — Pass the exchange to the store in the MCP server server so its writes match listener and scheme out-of-space rules and publish their exchange (ADR-0168)

## Goal

The MCP server server hands its ExchangeContext to the store (ContentCaller (ContentCaller.ForExchange)), so an out-of-space rule scoped by listener or scheme fires for its writes and its store changes carry the exchange's ID.

## Context

ADR-0168 decision 5 (Documentation/Planning/Decisions/ADR-0168-how-the-content-store-and-the-mail-store-learn-the-exchange-behind-a-write.md). Replace each ContentCaller.Anonymous/ForAccount (or mailbox-store call) in Surl.Extended.Protocol.McpServer with the exchange-carrying form once BL-1249 is Done. No wire byte changes.

## Acceptance criteria

- [x] A unit test proves an `out-of-space;listener=<this server's listen URL>` rule fires for this server's write.
- [x] A unit test proves the published StoreChangedEvent for this server's write carries the exchange's ExchangeId.
- [x] Build clean; fast tests green; the library's quality gates held.

## Notes

- The session is created on the `initialize` exchange, so its writes carry that exchange's ID, listen URL and scheme; later requests on the session reuse that caller. The stdio session has no exchange and stays anonymous.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. MCP server hands its exchange to the content store
