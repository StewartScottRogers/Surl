---
id: BL-2079
title: Say in the reset_server and restore_server descriptions that OCI and MCP tokens stop working
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2079 — Say in the reset_server and restore_server descriptions that OCI and MCP tokens stop working

## Goal

An agent reading the `surl-mcp` tool list learns that `reset_server` and `restore_server` end every OCI token and
all MCP OAuth state and HTTP sessions, and what else each one changes.

## Context

Found while working BL-2059, read from source on 2026-11-28. Both calls also revoke OCI tokens, clear MCP OAuth state
and sessions, and discard open uploads (ADR-0167 decisions 2 and 4 and Amendment 4, point 2;
`Application/Surl.Composition.UnitLibrary/CLAUDE.md`). `ResetServerTool.Description` names files, mail, messages,
accounts, Redis keys, the LDAP directory and DNS zones only; `RestoreServerTool.Description` names files, mail,
messages and accounts only. Confirm from `ComposedStores`' registered store order which stores each call covers.

## Acceptance criteria

- [ ] `ResetServerTool.Description` says that OCI tokens, MCP OAuth state and MCP HTTP sessions are ended and that open uploads are discarded, and a test in `Application/Surl.Mcp.UnitTests` asserts it.
- [ ] `RestoreServerTool.Description` names every store a restore replaces or clears, tokens, sessions and open uploads included, and a test in `Application/Surl.Mcp.UnitTests` asserts it.
- [ ] `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`'s `reset_server` and `restore_server` entry says the same.

## Notes

## Log

- 2026-11-28: Created.
