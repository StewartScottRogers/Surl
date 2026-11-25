---
id: BL-1678
title: Fix the import ordering dotnet format reports in ten files
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: []
requirement: none
created: 2026-11-24
completed:
---
# BL-1678 — Fix the import ordering dotnet format reports in ten files

## Goal

`dotnet format --verify-no-changes` reports no `IMPORTS`, `WHITESPACE` or `FINALNEWLINE` error. The title's "import ordering" and "ten files" understate this: the whole list below is the task.

## Context

On 2026-11-24 (during BL-1673) it exited 2. Besides what BL-1673 fixed, it reported:

- `IMPORTS`: `ComposedStores.cs`, `ControlEventRequests.cs`, `LdapDirectoryFile.cs`, `McpAuthorizationGrants.cs`, `ServerTlsHandshake.cs`, `SftpSessionTests.Writes.cs`.
- `FINALNEWLINE` and `WHITESPACE`: about twenty places, among them `CookieHandler.cs`, `ProxyRoutingProtocolServer.cs`, `LdapDirectory.cs` and `XmppStanzaRouterTests.cs`; run the command for the full list.
- `ENDOFLINE`: many files in lane 1's worktree that are LF on disk and in the index while `.editorconfig` asks CRLF (`git ls-files --eol`: `i/lf w/lf`). A fresh CRLF checkout may not show them; they are not part of this task unless they show in one.

`touches` is empty on purpose: the files are spread over many projects, so the task runs alone.

## Acceptance criteria

- [ ] `dotnet format --verify-no-changes` reports no `IMPORTS`, `WHITESPACE` or `FINALNEWLINE` error.
- [ ] The diff changes only whitespace, final newlines and `using` directive order.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing.
