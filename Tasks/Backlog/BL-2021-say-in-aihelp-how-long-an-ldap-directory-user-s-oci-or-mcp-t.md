---
id: BL-2021
title: Say in --aihelp how long an LDAP directory user's OCI or MCP token is honoured
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2004, BL-2019, BL-2020]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2021 — Say in --aihelp how long an LDAP directory user's OCI or MCP token is honoured

## Goal

`surl --aihelp auth` says how long a `--user-ldap` user's OCI or MCP token is honoured.

## Context

ADR-0167 Amendment 3 (BL-2003), points 2 to 4. BL-2004, BL-2019 and BL-2020 build the behaviour; this task adds it to
the `--user-ldap` paragraph of the `auth` topic in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`, written from
the code as those tasks left it. A test author needs it: disabling a user in the directory does not end that user's
tokens at once.

## Acceptance criteria

- [ ] The `--user-ldap` paragraph says that an OCI or MCP token of a directory user is honoured for at most one hour after the name's last accepted directory login, a refresh not counting as a login, within the token's own lifetime.
- [ ] It says that disabling the user in the directory ends no issued token, that a sessions reset does, and that a name the accounts have held while surl runs is refused such tokens.
- [ ] A test in `Application/Surl.Cli.UnitTests` finds the one-hour statement in the `auth` topic's text.
- [ ] `dotnet build -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
