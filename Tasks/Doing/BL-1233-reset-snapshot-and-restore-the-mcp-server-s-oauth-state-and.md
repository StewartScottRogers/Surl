---
id: BL-1233
title: Reset, snapshot and restore the MCP server's OAuth state and sessions and judge tokens against the account book
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1216, BL-1228]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1233 — Reset, snapshot and restore the MCP server's OAuth state and sessions and judge tokens against the account book

## Goal

A reset or restore clears the MCP server's OAuth clients, codes and tokens and ends its HTTP sessions, and a removed account's token or session stops working at once.

## Context

ADR-0167 decisions 2, 3 and 4. `McpAuthorizationGrants`, `McpHttpSessionTable`, `McpHttpRequestAnswerer.LogInAsync`. Register one `StoreKind.Sessions` registration named `mcp` from Surl.Composition. `AccountOf` and refresh redemption must check the account still exists in the account book on each use; a session opened by a removed account is refused on its next request.

## Acceptance criteria

- [ ] After reset, a registered client id answers `invalid_client`, an earlier access token is judged as no token, and an earlier `Mcp-Session-Id` is answered 404.
- [ ] A token or session of an account removed afterwards is refused on its next request; an anonymous one is not.
- [ ] Snapshot reports 0 bytes; restore clears as reset does; one `StoreChangedEvent`.
- [ ] 100% line and branch coverage for the changed code.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
