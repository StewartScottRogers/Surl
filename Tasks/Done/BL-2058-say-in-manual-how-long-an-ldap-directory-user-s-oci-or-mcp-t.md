---
id: BL-2058
title: Say in --manual how long an LDAP directory user's OCI or MCP token is honoured
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2058 — Say in --manual how long an LDAP directory user's OCI or MCP token is honoured

## Goal

`surl --manual` says, in its `--user-ldap` paragraph, how long a directory user's OCI or MCP token is honoured.

## Context

Found while working BL-2021, which said it in `--aihelp auth` only. `Surl.Cli.UnitLibrary/CLAUDE.md` keeps
`ManualText` and `AiHelpProse` describing the same behaviour. Write the manual's text from the sentences BL-2021
added to the `--user-ldap` paragraph in `AiHelpProse.cs` (ADR-0167, Amendment 3).

## Acceptance criteria

- [x] The `--user-ldap` paragraph of `ManualText.cs` states the one-hour limit, that a refresh and a refused login do not renew it, that disabling the user in the directory ends no issued token, what a reset or restore through `--control` does (and that `--directory` or `--share` refuses it), and the rule for a name the accounts have held.
- [x] A test in `Application/Surl.Cli.UnitTests` finds the one-hour statement in `ManualText.Text`.
- [x] `dotnet build -warnaserror` is clean and the fast tests pass.

## Notes

- The `--user-ldap` paragraph of `ManualText.Text` now ends with `AiHelpProse`'s four BL-2021 sentences, word for
  word without the Markdown backticks (ADR-0167 Amendment 3).
- `ManualTextTests.Text_SaysHowLongADirectoryUsersOciOrMcpTokenIsHonoured` failed before the text change; the
  test that pins the whole manual line by line grew by the same lines.
- Pipeline default taken: the change is 12 lines of manual text and its tests, so the session wrote the plan, the
  test and the text itself and delegated no `/feature` stage. No ADR: no behaviour changed. No upstream curl
  measurement: upstream curl has neither `oci` nor `mcp`.
- Verify: build `-warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 0 failed
  (`Surl.Cli.UnitTests` 2062 passed).

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. surl --manual's --user-ldap paragraph now says how long a directory user's OCI or MCP token is honoured
