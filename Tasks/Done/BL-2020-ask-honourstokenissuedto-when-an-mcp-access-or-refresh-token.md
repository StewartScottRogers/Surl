---
id: BL-2020
title: Ask HonoursTokenIssuedTo when an MCP access or refresh token is used
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2018]
touches: [Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2020 — Ask HonoursTokenIssuedTo when an MCP access or refresh token is used

## Goal

The MCP server asks `IAuthenticationPolicy.HonoursTokenIssuedTo` whether an access or refresh token it issued to an
account is still honoured.

## Context

ADR-0167 Amendment 3 (BL-2003), point 6. BL-2018 adds the member, whose default is `NamesAccount`.
`McpAuthorizationGrants.AccountOf` and `RedeemRefreshToken`
(`Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary`) call `NamesAccount` today, so tokens issued to an
LDAP directory user are refused. BL-2004 makes `Surl.Authentication` answer `true` for such a user; this task only
changes what the server asks, and its tests use `UnitTestBearerAuthenticationPolicy`. A refused refresh token is
still consumed, and redeeming an authorization code still asks nothing. Judged by the MCP specification, no upstream
curl measurement.

Stand-in: no new store, event or fault. A test takes access away with `remove_account`, reset or restore of the
accounts, or a sessions reset, which deletes every client, code and token.

## Acceptance criteria

- [x] A test whose policy honours the name (`HonoursTokenIssuedTo` true) while `NamesAccount` is false is served with the access token and rotates the refresh token; it was seen failing before the change (said in Notes) and passes after.
- [x] A test whose policy answers `HonoursTokenIssuedTo` false while `NamesAccount` is true answers `401` to the access token and `400 invalid_grant` to the refresh token.
- [x] `McpAuthorizationGrants` calls `HonoursTokenIssuedTo` and no longer calls `NamesAccount`; the existing removed-account tests pass with their expected values unchanged.
- [x] The XML and code comments of the library and its `CLAUDE.md` ("Reset and accounts") name `HonoursTokenIssuedTo` and say a directory user's token may be honoured (ADR-0167 Amendment 3).
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and the library stays at 100% line and branch coverage.

## Notes

- 2026-11-27: Plan: `McpAuthorizationGrants.AccountOf` and `RedeemRefreshToken` ask `HonoursTokenIssuedTo`
  instead of `NamesAccount`; nothing else changes. The change is two calls, so the `/feature` stages (tests first,
  implement, verify, review of the diff) ran in this session with no subagents. No conformance run: the server is
  judged by the MCP specification, as Context says. No ADR: ADR-0167 Amendment 3 already decides it.
- 2026-11-27: The four new tests in `McpResetTests` all failed before the change (`401` and `400 invalid_grant` for
  an honoured name the account book does not name; `200` for a refused named account) and pass after it. The
  refused-refresh-token test also checks the token is consumed.
- 2026-11-27: `UnitTestBearerAuthenticationPolicy` gained `HonoursTokenIssuedTo`, with `HonouredTokenAccounts` and
  `RefusedTokenAccounts`; with both empty it answers as `NamesAccount`, so earlier tests are unchanged.
- 2026-11-27: `dotnet build -warnaserror` 0 warnings; fast tests 30459 passed, 0 failed, 3 skipped;
  `Measure-CodeQuality.ps1` on the library: 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The MCP server asks HonoursTokenIssuedTo when an access or refresh token it issued is used
