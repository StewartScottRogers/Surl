---
id: BL-1479
title: Warn when remove_proxy_account leaves the proxy with no account
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1478]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1479 — Warn when remove_proxy_account leaves the proxy with no account

## Goal

An agent that removes a server's last proxy account with `remove_proxy_account` is told the proxy now needs no login,
as `remove_account` tells it for server accounts (BL-1391).

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 3.
- `Application/Surl.Mcp.UnitLibrary/RemoveProxyAccountTool.cs:41-54` answers `{"user"}` whatever is left.
- The pattern to copy: `Application/Surl.Mcp.UnitLibrary/RemoveAccountTool.cs:43-63` (a `warning` member and a second text block
  when nothing is left) and its description, pinned by `Application/Surl.Mcp.UnitTests/DeleteAndRemoveToolsTests.cs:76`.
- BL-1478 adds `SurlServerProxyAccounts.HasAccounts`; read it after a successful `Remove`.
- Wording: `server <id> has no proxy account left and its proxy now needs no login`.
- Stand-in: no event, store or fault change.

## Acceptance criteria

- [ ] `remove_proxy_account` removing the last proxy account answers success with `{"user", "warning"}` and a second text block
      `server s1 has no proxy account left and its proxy now needs no login`; removing a non-last one keeps `{"user"}`. Named
      tests in `Application/Surl.Mcp.UnitTests/ProxyAndTlsSrpAccountToolsTests.cs` pin both.
- [ ] `RemoveProxyAccountTool.Description` says removing the last proxy account opens the proxy, and
      `ProgramTests`' description row still passes.
- [ ] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean, the fast tests are green and `Surl.Mcp.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-20: Created.
