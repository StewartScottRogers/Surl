---
id: BL-1492
title: Add the three set-password MCP tools per ADR-0132 amendment 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1482, BL-1493, BL-1494, BL-1491, BL-1505]
touches: [Application/Surl.Mcp, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-21
---
# BL-1492 — Add the three set-password MCP tools per ADR-0132 amendment 1

## Goal

`surl-mcp` gains `set_account_password`, `set_proxy_account_password` and `set_tls_srp_account_password`.

## Context

- ADR-0132 amendment 1 (`Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md`) states the rule, the answers and the events; follow it exactly.

## Acceptance criteria

- [x] Each tool takes `server_id`, `user`, `password`, answers `{"user"}` without the password, and answers `isError: true` with `user <name> has no account` for an unknown user; `tools/list` order and tests updated.
- [x] The tools appear in the MCP documentation that lists the others.

## Notes

- Added `Application/Surl.Mcp` to touches: `Program.cs` registers the tools, and its README and CLAUDE.md list them. No task in Doing touches it.
- Empty password is refused by the input schema (`minLength: 1`) as for the add tools; any `ArgumentException` from `SetPassword` is therefore the unknown user.

## Log
- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Backlog. Needs Surl.Api SetPassword on the account, proxy and TLS-SRP views: waits for BL-1491 and BL-1505
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. Added set_account_password, set_proxy_account_password and set_tls_srp_account_password
