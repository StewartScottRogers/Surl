---
id: BL-1281
title: Surl.Mcp: tools to add and remove proxy and TLS-SRP accounts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244, BL-1279]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-061
created: 2026-11-16
completed:
---
# BL-1281 — Surl.Mcp: tools to add and remove proxy and TLS-SRP accounts

## Goal

An AI agent adds and removes proxy and TLS-SRP accounts through `surl-mcp` tools.

## Context

ADR-0170 decision 4, after BL-1279. Shaped like `add_account` and `remove_account`; the tool list and its tests grow by four.

## Acceptance criteria

- [ ] Tools `add_proxy_account`, `remove_proxy_account`, `add_tls_srp_account`, `remove_tls_srp_account` are listed with descriptions and input schemas.
- [ ] Each calls the matching `ComposedStores` member and reports a refusal when the store is absent.
- [ ] The tool-list completeness tests pass; the project keeps its quality gates.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
