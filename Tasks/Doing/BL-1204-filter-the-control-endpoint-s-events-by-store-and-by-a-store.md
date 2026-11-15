---
id: BL-1204
title: Filter the control endpoint's events by store and by a store change's name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1194]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: FR-065
created: 2026-11-15
completed:
---
# BL-1204 — Filter the control endpoint's events by store and by a store change's name

## Goal

`GET /v1/events` and `GET /v1/events/wait` take `store` and the name query BL-1194's ADR decides, so a non-.NET suite can wait for one file, mailbox or queue change.

## Context

- Decision: the ADR filed by BL-1194 (query names, snake_case `store` values, and exact or segment-prefix name matching).
- Where: `Application/Surl.Control.UnitLibrary/ControlEventQuery.cs` holds `After`, `Kind`, `Scheme`, `ListenUrl` and `Account` with their `Matches*` methods. `ControlEventRequests.cs` reads the query string with `ControlQuery`. The filtering is done inside the control library, so no composition change is needed.
- This closes a parity gap with `SurlEventQuery.Store` in `Surl.Api`.

## Acceptance criteria

- [ ] `ControlEventQuery` has `Store` and the ADR's name member, matched against `StoreChangedEvent` only. An event of another kind never matches a query that sets either.
- [ ] Tests in `Application/Surl.Control.UnitTests` show `GET /v1/events?store=content` returns only content changes, and `?name=<path>` returns only that path's change.
- [ ] A test shows `GET /v1/events/wait` with a name nothing touched answers `{"timed_out": true}` after `timeout_ms` on the fake `TimeProvider`.
- [ ] A test shows an unknown `store` value answers `400 bad_request`.
- [ ] `dotnet build Application/Surl.Control.UnitLibrary -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
