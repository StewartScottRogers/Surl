---
id: BL-722
title: Serve the MCP server's OAuth 2.1 authorization server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-466, BL-463]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Documentation/Conformance/Extended/McpServer.md, Documentation/Planning/Decisions/ADR-0111-how-the-mcp-server-answers-mcp-mcps-and-mcp-stdio.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
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

- [x] Named tests show: the RFC 8414 metadata document; registering a client (RFC 7591); an authorization-code
      exchange with PKCE `S256` returning a token that the MCP endpoint accepts; a wrong `code_verifier` refused
      `invalid_grant`; a token issued for another `resource` refused `401` `invalid_token` at the endpoint;
      `client_credentials`; a refresh token rotated on use; an expired token refused.
- [x] `Documentation/Conformance/Extended/McpServer.md` gains rows for RFC 8414, RFC 7591, RFC 7636 and RFC 8707,
      each covered by a named test, and `MCP:Auth-10` names them.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-463 (ADR-0111 decision 6.3), 2026-11-05.
- 2026-11-06 (lane 7): delivered directly from ADR-0111 decision 6.3, which already fixed the endpoints, grants,
  token shape and lifetimes, so no separate architect plan was needed. The choices the RFCs left open are
  recorded as ADR-0111 decision 6.4 ("Decided by Claude under Stewart's delegation"):
  - Passwords are checked through `IAuthenticationPolicy.CheckPasswordLoginAsync`, not
    `IHttpAuthenticationSession` as this task's Context said: an MCP server's HTTP session judges Bearer only, so
    it would refuse every Basic login. Plaintext refusal stays the policy's.
  - Issued tokens are looked up before the policy's session; anything else falls through to it, so expired or
    foreign-resource tokens get the usual `401 invalid_token` and the failed-login delay.
  - Clients are all public (`none`), registration is open; `client_credentials` authenticates an account.
  - Codes live 10 minutes and die on the first redemption attempt; absent `resource` means this endpoint.
  - 1000 live entries per kind (clients, codes, access, refresh), `503 Retry-After: 60` past it.
  - Origin is checked on every authorization-server path; POST bodies are bounded by `--max-message`.
- Added ADR-0111 to `touches` to record decision 6.4 and drop its "endpoints answer 404 until BL-722" text; no
  task in Doing names it.
- `McpProtocolServer`'s `sessionIdSource` parameter is now `randomSource`, as it also feeds client IDs, codes and
  tokens; nothing composes the server yet (BL-468), so no caller changed.
- Answer building moved from `McpHttpRequestAnswerer` to `McpHttpAnswerWriter`, field reading to
  `McpHttpFields`, so the endpoint and the authorization server share them.
- PKCE values are RFC 7636 appendix B's. Library coverage measured with the coverage collector: 100% line,
  100% branch.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. mcp and mcps serve their own OAuth 2.1 authorization server: RFC 8414 metadata, RFC 7591 registration, PKCE S256 codes, client_credentials, rotated refresh tokens, resource-bound tokens accepted at the endpoint
