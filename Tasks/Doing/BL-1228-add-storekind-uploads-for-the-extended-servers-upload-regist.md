---
id: BL-1228
title: Add StoreKind.Uploads for the extended servers' upload registrations
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1216]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1228 — Add StoreKind.Uploads for the extended servers' upload registrations

## Goal

`StoreKind` has an `Uploads` member, so a server can publish a store change for its in-progress uploads.

## Context

ADR-0167 decision 3. Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreKind.cs; add the member last, with its XML doc, and cover it in the tests that enumerate `StoreKind`. Check anything that switches over `StoreKind` (AiHelp, control endpoint, Mcp) with a build; if one lives outside touches, add that project to touches and say why under Notes.

## Acceptance criteria

- [ ] `StoreKind.Uploads` exists with an XML doc comment and a test pins it.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
