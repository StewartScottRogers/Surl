---
id: BL-2054
title: Say in --manual how long an LDAP directory user's OCI or MCP token is honoured
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2054 — Say in --manual how long an LDAP directory user's OCI or MCP token is honoured

## Goal

`surl --manual` says, in its `--user-ldap` paragraph, how long a directory user's OCI or MCP token is honoured.

## Context

Found while working BL-2021, which said it in `--aihelp auth` only. `Surl.Cli.UnitLibrary/CLAUDE.md` keeps
`ManualText` and `AiHelpProse` describing the same behaviour. Write the manual's text from the sentences BL-2021
added to the `--user-ldap` paragraph in `AiHelpProse.cs` (ADR-0167, Amendment 3).

## Acceptance criteria

- [ ] The `--user-ldap` paragraph of `ManualText.cs` states the one-hour limit, that a refresh and a refused login do not renew it, that disabling the user in the directory ends no issued token, what a reset or restore through `--control` does (and that `--directory` or `--share` refuses it), and the rule for a name the accounts have held.
- [ ] A test in `Application/Surl.Cli.UnitTests` finds the one-hour statement in `ManualText.Text`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
