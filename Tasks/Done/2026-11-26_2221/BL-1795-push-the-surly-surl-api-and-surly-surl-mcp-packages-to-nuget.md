---
id: BL-1795
title: Push the Surly.Surl.Api and Surly.Surl.Mcp packages to nuget.org on each release tag
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/release.yml, Documentation/Planning/Decisions/ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md, Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1795 — Push the Surly.Surl.Api and Surly.Surl.Mcp packages to nuget.org on each release tag

## Goal

Each `v*` tag's Release workflow pushes `Surly.Surl.Api` and `Surly.Surl.Mcp` at the tag's version to nuget.org.

## Context

Stewart, 2026-11-26: push the NuGet packages to nuget.org; his nuget.org user name is `StewartScottRogers`, and he sets up a trusted publishing policy there for owner `StewartScottRogers`, repository `Surl`, workflow `release.yml`. BL-1789 already attaches both packages to each GitHub release.

## Acceptance criteria

- [x] `release.yml`'s release job has `id-token: write`, logs in with `NuGet/login@v1` as `StewartScottRogers` and pushes both packages by exact name with `--skip-duplicate`, after the GitHub release is created; a dry run pushes nothing.
- [x] The workflow's header and `pack` comments say the packages are pushed to nuget.org.
- [x] ADR-0099 and ADR-0103 carry amendments recording Stewart's decision and the mechanism.
- [x] BL-1790 is amended to state the push in the README, download page, overview, roadmap and csproj comments.

## Notes

The first real proof is the next tag's Release run: its `Push the packages to nuget.org` step and the packages appearing at https://www.nuget.org/packages/Surly.Surl.Api. If the login step fails, the trusted publishing policy on nuget.org is missing or names another workflow.

## Notes (2)

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. Push step written in release.yml; ADRs and BL-1790 amended. Proven on the next tag.
