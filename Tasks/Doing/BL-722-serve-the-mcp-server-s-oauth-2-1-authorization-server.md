---
id: BL-722
title: Serve the MCP server's OAuth 2.1 authorization server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-466, BL-463]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Documentation/Conformance/Extended/McpServer.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-722 — Serve the MCP server's OAuth 2.1 authorization server

## Goal

`surl mcp://` and `mcps://` serve their own OAuth 2.1 authorization server, as ADR-0111 decision 6.3 decides: RFC 8414
metadata, RFC 7591 dynamic client registration, the authorization-code grant with PKCE `S256`, `client_credentials`,
refresh tokens, and access tokens bound to the RFC 8707 `resource`, which the endpoint then accepts.

## Context

- Decision: ADR-0111 decision 6 (6.1 checks issued tokens beside configured ones; 6.2's metadata names the issuer
  `<scheme>://<authority>`; 6.3 the endpoints, grants, token shape and lifetimes).
- MCP 2025-06-18 "Base Protocol > Authorization"; OAuth 2.1 (draft-ietf-oauth-v2-1-13); RFC 8414; RFC 7591;
  RFC 7636 (PKCE); RFC 8707 (resource indicators). Expected bytes come from these documents only (ADR-0083 decision 1).
- `/authorize` authenticates the resource owner with HTTP Basic against Surl's accounts through
  `IHttpAuthenticationSession` (ADR-0084 decision 4); redirect URIs only `https` or loopback `http`.
- Random bytes from an injected source; expiry on an injected `TimeProvider`; tokens in memory only.
- Builds on BL-466's Streamable HTTP endpoint in the same library.

## Acceptance criteria

- [ ] Named tests show: the RFC 8414 metadata document; registering a client (RFC 7591); an authorization-code
      exchange with PKCE `S256` returning a token that the MCP endpoint accepts; a wrong `code_verifier` refused
      `invalid_grant`; a token issued for another `resource` refused `401` `invalid_token` at the endpoint;
      `client_credentials`; a refresh token rotated on use; an expired token refused.
- [ ] `Documentation/Conformance/Extended/McpServer.md` gains rows for RFC 8414, RFC 7591, RFC 7636 and RFC 8707,
      each covered by a named test, and `MCP:Auth-10` names them.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-463 (ADR-0111 decision 6.3), 2026-11-05.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
