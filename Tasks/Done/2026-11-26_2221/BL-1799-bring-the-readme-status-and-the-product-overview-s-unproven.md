---
id: BL-1799
title: Bring the README status and the product overview's unproven list up to date with the pinned upstream curl builds and registered MCP tools
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1799 — Bring the README status and the product overview's unproven list up to date with the pinned upstream curl builds and registered MCP tools

## Goal

The README's status, Phase 6 and MCP tool text and the product overview's Phase 6 proof text
describe the repository as it is.

## Context

Found by BL-1790, which changed release statements only. `README.md`: the status section is
dated 2026-11-05 with old counts, the Phase 6 item says upstream curl is pinned for only three
platforms, and the `surl-mcp` tool list is shorter than the tools it registers
(`Application/Surl.Mcp/CLAUDE.md`). `Product-Overview.md`: "What is not proven" and the runtime
identifier table say three platforms have no pinned build and BL-361 and BL-1059 are open, and
its pass-rate table is older than `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md`.
Compare `UpstreamCurlBuilds.json`, `.github/workflows/release.yml` and the Roadmap.

## Acceptance criteria

- [x] Neither file says a platform lacks a pinned upstream curl build that
      `UpstreamCurlBuilds.json` pins.
- [x] Neither file calls a task open that is in `Done` or its archive.
- [x] The README's `surl-mcp` tool list names every tool `Surl.Mcp` registers.

## Notes

- Delivered by `align-and-document`: both files updated. The README `surl-mcp` list is a table of the 40 tools `Application/Surl.Mcp/Program.cs` registers (lines 141-180).
- Checked: `UpstreamCurlBuilds.json` pins all six platforms; `release.yml` runs conformance on every runtime identifier but `win-x64`; every `BL-` ID either file cites is in `Done` or its archives.
- `dotnet build` clean (0 warnings); fast tests: 30,128 passed in 83 assemblies. No `.cs` or project file changed, so no format run.
- Out of scope, filed: BL-1806 (protocol server count, `Protocols/Proxy/` row) and BL-1807 (`Surl.Mcp/CLAUDE.md` tool list). If the shift renumbers them, find them by title.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. README status, Phase 6 and MCP tool text and the product overview proof text match the pinned builds, pass rates and 40 registered tools
