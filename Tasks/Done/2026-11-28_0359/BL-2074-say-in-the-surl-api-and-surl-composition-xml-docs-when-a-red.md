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
completed: 2026-11-28
---
# BL-2074 — Say in the Surl.Api and Surl.Composition XML docs when a Redis, LDAP or DNS store exists

## Goal

The XML docs say a Redis, LDAP or DNS store exists when a listen URL serves it, and the `SurlServer.ResetAsync` summary describes only states a host can reach.

## Context

Found while working BL-2034 (2026-11-28), read from source. A store exists only when a listen URL serves it (`SurlServerComposition.Start.cs`), but `SurlServerKeys.cs`, `SurlServerDirectory.cs`, `SurlServerZones.cs` and `ComposedStores.cs` say no server "is composed" or "composes no" server. `SurlServer.ResetAsync`'s summary says a reset leaves the LDAP entries and DNS zones read from the data directory, but with a data directory the host's reset throws at the content store first (ADR-0158 decision 4 describes the store level). Documentation only.

## Acceptance criteria

- [x] Each XML comment in the four files about an absent Redis, LDAP or DNS store says no listen URL serves it.
- [x] The `SurlServer.ResetAsync` summary says a server with a data directory refuses the reset, and describes no state the host cannot reach.
- [x] The diff changes only `///` lines, and `dotnet build` is clean.

## Notes

- Delivered by `align-and-document`: five files, `///` lines only (the four named plus `SurlServer.cs`).
- The `ResetAsync` summary no longer mentions entries or zones read from a data directory: a server with one refuses the reset at the content store, first in the order.
- The `ResetAsync` exception doc keeps "the stores before it are reset": the mail store, second, can also refuse through a host, when `SurlServerOptions.TimeProvider` is near 2106-02-07T06:28:15Z (`MailboxStore.Reset`).
- Verified: `dotnet build -warnaserror` and `dotnet format --verify-no-changes` clean; fast tests 30769 passed, 3 skipped, 0 failed. Integration tests not run (comments only).
- Left outside this task's four files and filed: BL-2097 (`SurlServerOptions`, `SurlServerKeys.SetAsync` and `Surl.Composition` `CLAUDE.md` statements), BL-2098 (`set_key` description omits `rediss`).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The Surl.Api and Surl.Composition XML docs say a Redis, LDAP or DNS store exists only when a listen URL serves it, and ResetAsync describes only reachable states
