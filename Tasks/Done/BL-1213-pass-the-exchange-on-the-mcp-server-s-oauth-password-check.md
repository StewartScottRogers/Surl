---
id: BL-1213
title: Pass the exchange on the MCP server's OAuth password check
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1213 — Pass the exchange on the MCP server's OAuth password check

## Goal

The extended MCP server's OAuth password check (authorize and token endpoints) carries its exchange, so each `LoginJudgedEvent` has the exchange ID and a `refuse-login;listener=mcp://...` rule applies.

## Context

- Found by the stand-in audit of 2026-11-15. `Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary/McpPasswordCheck.cs` line 37 builds `PasswordLogin(scheme, userName, password, tlsSession)` without the `Exchange` argument. `McpHttpRequestAnswerer.cs` line 53 constructs `McpPasswordCheck(policy, context.Scheme, tlsSession)`, two lines after it already passes `LoginExchange.Of(context)` to `StartHttpConnection` (line 49).
- Effect: `LoginJudgedEvent.ExchangeId` is null for OAuth logins and a listener-scoped `refuse-login` never matches them.

## Acceptance criteria

- [x] `McpPasswordCheck` takes the `LoginExchange` and passes it on every `PasswordLogin`. `McpHttpRequestAnswerer` hands it `LoginExchange.Of(context)`.
- [x] A test in `Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests` shows the policy receives an `Exchange` whose ID and scheme are the connection's, for a Basic login at the authorize endpoint.
- [x] `dotnet build Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary -warnaserror` is clean and the fast tests pass.

## Notes

- `McpPasswordCheck` takes a fourth constructor argument, the `LoginExchange`; the answerer builds it once and shares it with `StartHttpConnection`. Test: `ServeAsync_BasicLoginAtAuthorize_PassesTheConnectionsExchangeToThePolicy` (304 tests pass).

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. MCP OAuth password checks carry the exchange, so LoginJudgedEvent has its ID and listener-scoped refuse-login applies
