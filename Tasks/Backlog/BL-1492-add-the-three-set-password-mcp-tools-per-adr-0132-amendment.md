---
id: BL-1492
title: Add the three set-password MCP tools per ADR-0132 amendment 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1482, BL-1488, BL-1489]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1492 — Add the three set-password MCP tools per ADR-0132 amendment 1

## Goal

`surl-mcp` gains `set_account_password`, `set_proxy_account_password` and `set_tls_srp_account_password`.

## Context

- ADR-0132 amendment 1 (`Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md`) states the rule, the answers and the events; follow it exactly.

## Acceptance criteria

- [ ] Each tool takes `server_id`, `user`, `password`, answers `{"user"}` without the password, and answers `isError: true` with `user <name> has no account` for an unknown user; `tools/list` order and tests updated.
- [ ] The tools appear in the MCP documentation that lists the others.

## Notes

## Log
- 2026-11-20: Created.
