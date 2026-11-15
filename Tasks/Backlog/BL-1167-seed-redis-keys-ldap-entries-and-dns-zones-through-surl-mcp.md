---
id: BL-1167
title: Seed Redis keys, LDAP entries and DNS zones through Surl.Mcp tools
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1166]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1167 — Seed Redis keys, LDAP entries and DNS zones through Surl.Mcp tools

## Goal

An AI agent seeds Redis keys, LDAP entries and DNS zones on a server it started through `surl-mcp`, with the tools BL-1151's ADR names, in `tools/list` order after the existing seed tools.

## Context

- `Application/Surl.Mcp.UnitLibrary` has one class per tool (`SeedMailTool.cs`, `PutFileTool.cs`, `PublishMessageTool.cs`, `AddAccountTool.cs`), each over `RunningSurlServer`, which wraps a `Surl.Api` `SurlServer`; call BL-1166's `Surl.Api` members. `SeededContent.cs` holds the decoded-content bounds (16 MiB) and text-or-base64 rule.
- Rules (ADR-0103 decision 3, ADR-0124 decision 8): `server_id` required, `additionalProperties: false`, `structuredContent` equal to the text block, an unknown server or a refused seed `isError: true` with its text, no local path read.
- `reset_server`, `snapshot_server` and `restore_server` already cover the new stores through `Surl.Api`; no change to them.

## Acceptance criteria

- [ ] The tools BL-1151's ADR names are listed by `tools/list` with their JSON schemas, in the order the ADR gives.
- [ ] Named tests in `Application/Surl.Mcp.UnitTests`: each tool seeds its store on a running server and returns the ADR's result object; an unknown `server_id`, a missing required property, an extra property and a refused seed each return `isError: true`.
- [ ] A named test shows `reset_server` removes what each new tool seeded.
- [ ] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
