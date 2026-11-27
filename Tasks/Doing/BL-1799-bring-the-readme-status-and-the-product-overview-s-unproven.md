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
completed:
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

- [ ] Neither file says a platform lacks a pinned upstream curl build that
      `UpstreamCurlBuilds.json` pins.
- [ ] Neither file calls a task open that is in `Done` or its archive.
- [ ] The README's `surl-mcp` tool list names every tool `Surl.Mcp` registers.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
