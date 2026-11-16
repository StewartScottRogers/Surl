---
id: BL-1273
title: Show layer and streamId in Surl.Api exchange records, get_exchanges and GET /v1/exchanges, with --aihelp text (ADR-0169)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1267]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: FR-060
created: 2026-11-16
completed:
---
# BL-1273 — Show layer and streamId in the hosts

## Goal

A test reading exchanges through `Surl.Api`, `get_exchanges` or `GET /v1/exchanges` sees each byte chunk's `layer` and `streamId`.

## Context

ADR-0169 decision 2. Fields only add; a reader ignoring them sees what it saw before. If `--aihelp` text or `Surl.Cli` need a change, split that out with `task-planner`.

## Acceptance criteria

- [ ] `SurlExchangeRecord`'s chunks carry `Layer` and `StreamId`, wire chunks defaulting to `Wire` and `null`.
- [ ] `get_exchanges` and `GET /v1/exchanges` emit `layer` and `streamId` per chunk (a test each).
- [ ] Existing tests pass unchanged.
- [ ] 100% line and branch coverage kept; `dotnet build` clean.

## Notes

## Log

- 2026-11-16: Created.
