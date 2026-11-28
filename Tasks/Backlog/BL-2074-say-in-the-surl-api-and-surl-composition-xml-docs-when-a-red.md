---
id: BL-2074
title: Say in the Surl.Api and Surl.Composition XML docs when a Redis, LDAP or DNS store exists
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Composition.UnitLibrary]
requirement: FR-061
created: 2026-11-28
completed:
---
# BL-2074 — Say in the Surl.Api and Surl.Composition XML docs when a Redis, LDAP or DNS store exists

## Goal

The XML docs say a Redis, LDAP or DNS store exists when a listen URL serves it, and the `SurlServer.ResetAsync` summary describes only states a host can reach.

## Context

Found while working BL-2034 (2026-11-28), read from source. A store exists only when a listen URL serves it (`SurlServerComposition.Start.cs`), but `SurlServerKeys.cs`, `SurlServerDirectory.cs`, `SurlServerZones.cs` and `ComposedStores.cs` say no server "is composed" or "composes no" server. `SurlServer.ResetAsync`'s summary says a reset leaves the LDAP entries and DNS zones read from the data directory, but with a data directory the host's reset throws at the content store first (ADR-0158 decision 4 describes the store level). Documentation only.

## Acceptance criteria

- [ ] Each XML comment in the four files about an absent Redis, LDAP or DNS store says no listen URL serves it.
- [ ] The `SurlServer.ResetAsync` summary says a server with a data directory refuses the reset, and describes no state the host cannot reach.
- [ ] The diff changes only `///` lines, and `dotnet build` is clean.

## Notes

## Log

- 2026-11-28: Created.
