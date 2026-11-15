---
id: BL-1228
title: Add StoreKind.Uploads for the extended servers' upload registrations
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1216]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Mcp.UnitLibrary]
requirement: FR-061
created: 2026-11-15
completed: 2026-11-15
---
# BL-1228 — Add StoreKind.Uploads for the extended servers' upload registrations

## Goal

`StoreKind` has an `Uploads` member, so a server can publish a store change for its in-progress uploads.

## Context

ADR-0167 decision 3. Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreKind.cs; add the member last, with its XML doc, and cover it in the tests that enumerate `StoreKind`. Check anything that switches over `StoreKind` (AiHelp, control endpoint, Mcp) with a build; if one lives outside touches, add that project to touches and say why under Notes.

## Acceptance criteria

- [x] `StoreKind.Uploads` exists with an XML doc comment and a test pins it.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- Added `Application/Surl.Mcp.UnitLibrary` to touches: `EventToolFilter`'s get-events input schema lists every `StoreKind` in a hand-written enum, and `StoreEventFilterTests.Schema_StoreEnum_ListsEveryStoreKindInSnakeCase` failed until `"uploads"` was appended. No task in Doing touches it.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. StoreKind.Uploads exists (doc-commented, pinned by tests) and get_events' store enum lists it
