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
completed: 2026-11-28
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

- [x] `ResetServerTool.Description` says that OCI tokens, MCP OAuth state and MCP HTTP sessions are ended and that open uploads are discarded, and a test in `Application/Surl.Mcp.UnitTests` asserts it.
- [x] `RestoreServerTool.Description` names every store a restore replaces or clears, tokens, sessions and open uploads included, and a test in `Application/Surl.Mcp.UnitTests` asserts it.
- [x] `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`'s `reset_server` and `restore_server` entry says the same.

## Notes

- Stores confirmed from `ComposedStores`' registered order and each `*StoreRegistration`, and matched against the
  XML documentation of `SurlServer.ResetAsync`, `SnapshotAsync` and `RestoreAsync` (read from source, 2026-11-28):
  content, mail, bus, accounts, proxy accounts, TLS-SRP accounts, Redis keyspace, LDAP directory, AMQP 0-9-1
  exchanges, Kafka producers, sessions (MQTT 5, OCI tokens, MCP), DNS zones, uploads (S3, Azure, GCS, OCI).
- Choice: both descriptions list every store by name rather than "every store", since an agent sees only the
  description. A restore's list is split into what is replaced and what is cleared, as `RestoreAsync` behaves.
- The class XML comments of both tools say the same in brief. No ADR: no behaviour or design changed.
- New tests: `ResetServerToolTests`, `RestoreServerToolTests`. Build clean, `dotnet format` clean, fast tests green
  (`Surl.Mcp.UnitTests` 542 passed).
- Filed BL-2090: `snapshot_server`'s description names four stores only.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The reset_server and restore_server descriptions name every store each call changes, ended OCI tokens, MCP OAuth state, MCP HTTP sessions and discarded uploads included
