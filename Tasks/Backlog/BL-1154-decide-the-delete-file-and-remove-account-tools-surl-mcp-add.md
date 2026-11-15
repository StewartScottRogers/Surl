---
id: BL-1154
title: Decide the delete_file and remove_account tools Surl.Mcp adds beside the control endpoint's routes
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1154 — Decide the delete_file and remove_account tools Surl.Mcp adds beside the control endpoint's routes

## Goal

A new ADR, marked "Decided by Claude under Stewart's delegation", amends ADR-0124 decision 8 with the `Surl.Mcp` tools that bring it level with `Surl.Api` and the control endpoint: deleting a file and removing an account.

## Context

- Stand-in audit 2026-11-14, host parity:
  - `Surl.Api`: `SurlServerFiles.DeleteAsync` and `SurlServerAccounts.Remove` exist (`Application/Surl.Api/`).
  - Control endpoint: `DELETE /v1/files/{path}` and `DELETE /v1/accounts/{user}` exist (`Application/Surl.Control.UnitLibrary/ControlEndpoint.cs`, `Routes`).
  - `Surl.Mcp`: has `put_file` and `add_account` but no way to undo either (`Application/Surl.Mcp.UnitLibrary/`, one class per tool: `PutFileTool.cs`, `AddAccountTool.cs`).
- Also compare `Surl.Api`'s `Faults.Clear()` with the control endpoint and `Surl.Mcp`, which have only remove-one; decide whether a clear is added to either, or say why not.
- ADR-0103 decision 3 and ADR-0124 decision 8 rules apply: `server_id` required, `additionalProperties: false`, `structuredContent` equal to the text block, an unknown server `isError: true`, no password echoed, no local path read.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` (next free number) names each new tool, its properties and their bounds, its result object, its place in `tools/list` order, and its error results (unknown path or account; a disk store, if it matters).
- [ ] It states whether a "clear faults" tool or route is added to `Surl.Mcp` or the control endpoint, and why.
- [ ] ADR-0124 decision 8 gains an "Amended by" line pointing at the new ADR, and the new ADR names BL-1175 under "Acted on by".

## Notes

## Log

- 2026-11-14: Created.
