---
id: BL-1822
title: Complete the surl-mcp tool and control route lists in the stand-in wiki and glossary
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Surl-As-A-Stand-In.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1822 — Complete the surl-mcp tool and control route lists in the stand-in wiki and glossary

## Goal

The stand-in wiki page lists every tool `surl-mcp` registers and every route the control endpoint serves,
and the glossary's "MCP tool" row is true of the registered tools.

## Context

Found while running BL-1807 (checked against the code on 2026-11-26).

- `Documentation/Wiki/Surl-As-A-Stand-In.md`, "`Surl.Mcp`, for AI agents", lists 33 tools. `Application/Surl.Mcp/Program.cs`
  has 40 `registry.AddTool` calls. Missing: `add_proxy_account`, `remove_proxy_account`, `add_tls_srp_account`,
  `remove_tls_srp_account`, `set_account_password`, `set_proxy_account_password`, `set_tls_srp_account_password`
  (ADR-0170 decision 4, ADR-0132 amendment 1).
- The same page's control endpoint "Routes:" paragraph lacks routes in the table `ControlEndpoint.Routes`
  (`Application/Surl.Control.UnitLibrary/ControlEndpoint.cs`): `PUT /v1/accounts/<name>`, `PUT` and `DELETE
  /v1/bearer-token`, `POST /v1/proxy-accounts`, `PUT` and `DELETE /v1/proxy-accounts/<name>`, `POST
  /v1/tls-srp-accounts`, `PUT` and `DELETE /v1/tls-srp-accounts/<name>`, `GET /v1/zones` and
  `GET /v1/zones/<apex>`. The library's `CLAUDE.md` describes each.
- `Documentation/Wiki/Glossary.md`, row "MCP tool", names 18 tools as if they were all of them.

No store, event or fault applies: this task changes prose only.

## Acceptance criteria

- [x] The "`Surl.Mcp`, for AI agents" section names every tool registered by a `registry.AddTool` call in
      `Application/Surl.Mcp/Program.cs`.
- [x] The control endpoint section names every method and path in `ControlEndpoint.Routes`.
- [x] The glossary's "MCP tool" row either names every registered tool or says its names are examples and
      links to the table that lists them all (`Application/Surl.Mcp/README.md` or the root `README.md`).

## Notes

- Delegated to `align-and-document`. A PowerShell comparison found 40 registered tools and 40 names in the
  wiki section, and 39 method and path pairs in `ControlEndpoint.Routes` and 39 in the wiki prose, with no
  differences either way.
- The glossary row gives examples and links to the root `README.md` table, so a new tool does not make the
  row false. The table was compared too and lists the same 40.
- `dotnet build` clean (0 warnings) and the fast tests green on 2026-11-26. Prose only, so no `/verify`.
- Follow-up drift outside this task's `touches` filed as BL-1826, BL-1827 and BL-1828. The `Instructions`
  string in `Application/Surl.Mcp/Program.cs` is already BL-1825.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The stand-in wiki lists all 40 surl-mcp tools and every control endpoint route, and the glossary's MCP tool row gives examples and links to the full table
