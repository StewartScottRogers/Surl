---
id: BL-1168
title: Seed Redis keys, LDAP entries and DNS zones through the control endpoint
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1161, BL-1163, BL-1165]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-065
created: 2026-11-14
completed: 2026-11-15
---
# BL-1168 — Seed Redis keys, LDAP entries and DNS zones through the control endpoint

## Goal

A test suite not written in .NET seeds Redis keys, LDAP entries and DNS zones through the loopback control endpoint, on the routes BL-1151's ADR names, under `/v1/` with the bearer token.

## Context

- `Application/Surl.Control.UnitLibrary`: `IControlTarget.cs` (the operations), `ControlEndpoint.Routes` (the route table), `ControlStoreRequests.cs` (the store routes' handling), `ControlError.cs` (the `{"error","message"}` answers, `400`, `404`, `409`, `413`, `415`). The interface is implemented by `Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs`, which must gain the same members in the same task or the build breaks - that is why this task touches four projects; the composition change is forwarding only (about 30 lines).
- ADR-0124 decision 7's rules hold: bodies bounded at 16 MiB, the content type checked, no answer echoes the token.

## Acceptance criteria

- [x] `IControlTarget` and `ComposedControlTarget` gain the three seed operations, and `ControlEndpoint.Routes` the routes BL-1151's ADR names, with their content types.
- [x] Named tests in `Application/Surl.Control.UnitTests`: each route with the token seeds through a fake `IControlTarget` and answers as the ADR says; a wrong content type is `415`, a malformed body `400` with the reader's message, a body over 16 MiB `413`, and a request without the token `401`.
- [x] Named tests in `Application/Surl.Composition.UnitTests`: `ComposedControlTarget` forwards each seed to `ComposedStores` and a refused seed surfaces as the control endpoint's `400`.
- [x] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

- Routes as ADR-0158 decision 9: `PUT /v1/keys/{database}/{key}` (query `expire_ms`, any body type), `POST /v1/directory` and `PUT /v1/zones/{apex}` (`text/plain`). A bad database or `expire_ms` is 400 in the endpoint.
- Mapping in `ComposedControlTarget` (decided by Claude under Stewart's delegation): Redis `ArgumentException` is 400, `InvalidOperationException` (bound, full, no keyspace) is 409; LDAP refusals are 400 with the reader's text (the directory throws one exception type, so a duplicate DN is 400, not 409); DNS refusals for the zone bounds or a second zone for an apex are 409, other zone text 400, no zone set 409.
- Coverage not re-measured by the coverage collector; every new branch has a named test.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. The control endpoint seeds Redis keys, LDAP entries and DNS zones on PUT /v1/keys, POST /v1/directory and PUT /v1/zones
