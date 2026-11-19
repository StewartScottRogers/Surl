---
id: BL-1391
title: Warn when remove_account or SurlServerAccounts.Remove leaves a server with no account and so open to anyone
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-19
completed:
---
# BL-1391 — Warn when remove_account or SurlServerAccounts.Remove leaves a server with no account and so open to anyone

## Goal

An agent or test that removes a server's last account is told the server now needs no login, instead of a
login-only stand-in turning open silently.

## Context

- Surly cookbook bug report 2026-11-18 20:25, finding 3 (a question). On a server started with one account,
  `remove_account` on it succeeds, and afterwards an unauthenticated request gets `200`; `add_account` closes it
  again. This follows the console rule (no `--user` means no login needed), so removal stays allowed.
- `Application/Surl.Mcp.UnitLibrary/RemoveAccountTool.cs`; `Application/Surl.Api/SurlServerAccounts.cs:47`
  (`Remove(string userName)` returns `bool`).
- Secure by default (CLAUDE.md): anything that widens what a peer may do is off until asked for, and warned.
  Decided by Claude under Stewart's delegation: keep removal allowed and warn. Refusing would break tidy-up code
  that removes an account and then adds another.

## Acceptance criteria

- [ ] `remove_account` removing the last account answers success with an added line naming the server and saying it
      now needs no login (e.g. `server s1 has no account left and now needs no login`); a named test in
      `Surl.Mcp.UnitTests` pins it, and removing a non-last account keeps today's answer.
- [ ] The `remove_account` tool description says removing the last account opens the server.
- [ ] `Surl.Api`: the same fact is observable - the instance's event stream or log records the server becoming open,
      and the `Remove` XML doc and the `Surl.Api` README say so; a named test in `Surl.Api.UnitTests` pins it.
      `Remove`'s signature does not change.
- [ ] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- If the `Surl.Api` half grows past one task's size, split it off with task-planner rather than widening this.

## Log

- 2026-11-19: Created.
