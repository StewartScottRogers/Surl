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
completed:
---
# BL-1168 — Seed Redis keys, LDAP entries and DNS zones through the control endpoint

## Goal

A test suite not written in .NET seeds Redis keys, LDAP entries and DNS zones through the loopback control endpoint, on the routes BL-1151's ADR names, under `/v1/` with the bearer token.

## Context

- `Application/Surl.Control.UnitLibrary`: `IControlTarget.cs` (the operations), `ControlEndpoint.Routes` (the route table), `ControlStoreRequests.cs` (the store routes' handling), `ControlError.cs` (the `{"error","message"}` answers, `400`, `404`, `409`, `413`, `415`). The interface is implemented by `Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs`, which must gain the same members in the same task or the build breaks - that is why this task touches four projects; the composition change is forwarding only (about 30 lines).
- ADR-0124 decision 7's rules hold: bodies bounded at 16 MiB, the content type checked, no answer echoes the token.

## Acceptance criteria

- [ ] `IControlTarget` and `ComposedControlTarget` gain the three seed operations, and `ControlEndpoint.Routes` the routes BL-1151's ADR names, with their content types.
- [ ] Named tests in `Application/Surl.Control.UnitTests`: each route with the token seeds through a fake `IControlTarget` and answers as the ADR says; a wrong content type is `415`, a malformed body `400` with the reader's message, a body over 16 MiB `413`, and a request without the token `401`.
- [ ] Named tests in `Application/Surl.Composition.UnitTests`: `ComposedControlTarget` forwards each seed to `ComposedStores` and a refused seed surfaces as the control endpoint's `400`.
- [ ] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
