---
id: BL-1177
title: Document the control endpoint from Surl.Api and Surl.Mcp's delete_file and remove_account tools
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1175, BL-1176]
touches: [Documentation/Wiki/Surl-As-A-Stand-In.md, Application/Surl.Api/README.md, Application/Surl.Mcp/README.md, Documentation/Product/Requirements.md]
requirement: FR-065
created: 2026-11-14
completed: 2026-11-15
---
# BL-1177 — Document the control endpoint from Surl.Api and Surl.Mcp's delete_file and remove_account tools

## Goal

The documentation says, truly, that `Surl.Api` can serve the control endpoint (BL-1176) and that `surl-mcp` can delete a file and remove an account (BL-1175).

## Context

- `docs` pipeline (`align-and-document`), no behaviour change. Files: `Application/Surl.Api/README.md` (the options list and a short "Control endpoint from a .NET harness" paragraph), `Application/Surl.Mcp/README.md` (the tool list), `Documentation/Wiki/Surl-As-A-Stand-In.md` (sections "The control endpoint, for suites not written in .NET" and "`Surl.Mcp`, for AI agents"), `Documentation/Product/Requirements.md` rows FR-061 and FR-065 (FR-065 today ends "`Surl.Api` has no control-endpoint option").
- Use the exact names in the code as BL-1175 and BL-1176 left them; cite their tests for each example.

## Acceptance criteria

- [x] `Application/Surl.Api/README.md` lists `ControlListenUrl`, `ControlTokenFile` and `SurlServer.ControlUrl` with the refusals they inherit from `surl`.
- [x] `Application/Surl.Mcp/README.md` and the wiki's `Surl.Mcp` section list the new tool(s) in `tools/list` order.
- [x] The wiki's control-endpoint section says the endpoint can be started from `surl` or `Surl.Api`, with a `SurlServerOptions` example.
- [x] `Documentation/Product/Requirements.md` FR-065 no longer says `Surl.Api` has no control-endpoint option and names BL-1176 as built; FR-061 names BL-1175.

## Notes

Docs only, no .cs touched, so no build. FR-065 already named BL-1176 and the Surl.Api option; FR-061 now names BL-1175. Mcp README gained the other stores and fault tools too, so its list matches tools/list order.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Surl.Api control endpoint and Surl.Mcp delete_file/remove_account are documented in READMEs, wiki and FR-061/FR-065
