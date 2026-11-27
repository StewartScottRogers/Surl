---
id: BL-1823
title: Correct the surl-mcp tool descriptions in the MCP library's CLAUDE.md and the host README
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary/CLAUDE.md, Application/Surl.Mcp/README.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1823 — Correct the surl-mcp tool descriptions in the MCP library's CLAUDE.md and the host README

## Goal

`Application/Surl.Mcp.UnitLibrary/CLAUDE.md` names the two remove-account tools by their tool names, and
`Application/Surl.Mcp/README.md` gives the answer `set_key` returns.

## Context

Found while running BL-1807 (checked against the code on 2026-11-26).

- `Application/Surl.Mcp.UnitLibrary/CLAUDE.md` names the classes `RemoveProxyAccountTool` and
  `RemoveTlsSrpAccountTool` but never their tool names, `remove_proxy_account` and `remove_tls_srp_account`
  (their `Name` constants). Every other registered tool is named there.
- `Application/Surl.Mcp/README.md` says `set_key` answers `{key, byte_count}`. `SetKeyTool.cs` answers
  `{key or key_base64, database, event_name, byte_count}` (ADR-0158 amendment 1, BL-1405).

No store, event or fault applies: this task changes prose only.

## Acceptance criteria

- [x] `Application/Surl.Mcp.UnitLibrary/CLAUDE.md` contains `remove_proxy_account` and
      `remove_tls_srp_account`.
- [x] Every tool registered by a `registry.AddTool` call in `Application/Surl.Mcp/Program.cs` is named in
      `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`.
- [x] `Application/Surl.Mcp/README.md` says `set_key` answers `key` (or `key_base64` for a key that is not
      UTF-8), `database`, `event_name` and `byte_count`, as `SetKeyTool` writes them.

## Notes

- All 40 `registry.AddTool` registrations in `Program.cs` have their `Name` constant, in backticks, in the
  library `CLAUDE.md` (checked by script on 2026-11-26).
- The README also says `key_base64` is answered for a key holding a control character other than tab, CR and
  LF, because `SetKeyTool` writes the key through `ReadBackJson.WriteBytes`, which does so.
- Follow-ups filed: BL-1826 (other statements in these two files) and BL-1827 (byte rules and `set_key`'s
  16 MiB limit in the tool descriptions). The stand-in wiki's missing tool names are already BL-1822's.
- Prose only. `dotnet build`: 0 warnings, 0 errors. Fast tests: exit 0, no failures.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The MCP library CLAUDE.md names both remove-account tools and the host README gives set_key's real answer
