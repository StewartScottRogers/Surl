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
completed: 2026-11-28
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

- [x] The `--user-ldap` paragraph says that an OCI or MCP token of a directory user is honoured for at most one hour after the name's last accepted directory login, a refresh not counting as a login, within the token's own lifetime.
- [x] It says that disabling the user in the directory ends no issued token, that a sessions reset does, and that a name the accounts have held while surl runs is refused such tokens.
- [x] A test in `Application/Surl.Cli.UnitTests` finds the one-hour statement in the `auth` topic's text.
- [x] `dotnet build -warnaserror` is clean and the fast tests pass.

## Notes

- Plan: prose appended to the `--user-ldap` paragraph in `AiHelpProse.cs`, one test, no behaviour change, no new ADR.
  Done in this session with no subagents; no conformance run (upstream curl has neither `oci` nor `mcp`).
- `Answer_Auth_SaysHowLongADirectoryUsersOciOrMcpTokenIsHonoured` was run and seen failing before the prose change.
- The text differs from ADR-0167 Amendment 3, point 3, where the code does: only a whole-instance reset or restore is
  public (`POST /v1/reset`, `SurlServer.ResetAsync`, the `surl-mcp` reset), and a content store on disk refuses it, so
  the help says it ends no token with `--directory` or `--share`. BL-2055 settles the ADR.
- `--manual` does not yet say any of this: BL-2054.
- Gates: build `-warnaserror` 0 warnings; `dotnet format --verify-no-changes` exit 0; fast tests 30589 passed,
  0 failed, 3 skipped. Only a prose constant changed, so coverage and complexity are unaffected.
## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-28: Doing -> Done. surl --aihelp auth says how long a --user-ldap user's OCI or MCP token is honoured
