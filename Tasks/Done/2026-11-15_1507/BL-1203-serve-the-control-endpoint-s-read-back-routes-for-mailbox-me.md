---
id: BL-1203
title: Serve the control endpoint's read-back routes for mailbox messages, files and bus messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1196, BL-1197, BL-1198]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-065
created: 2026-11-15
completed: 2026-11-15
---
# BL-1203 — Serve the control endpoint's read-back routes for mailbox messages, files and bus messages

## Goal

A test suite not written in .NET reads back mailbox messages, files and bus messages over the control endpoint's routes BL-1194's ADR names, behind the same bearer token.

## Context

- Decision: the ADR filed by BL-1194 (routes, answers, error codes, bounds). Composition members: BL-1196, BL-1197 and BL-1198 on `ComposedStores`.
- Where: `Application/Surl.Control.UnitLibrary`. Add the read members to `IControlTarget.cs` and the routes to `ControlStoreRequests.cs` / `ControlRoute.cs` beside the existing `PUT`/`DELETE /v1/files/{path}`. Implement them in `Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs`. BL-1168 changed the same four projects for seeding, so follow it.
- ADR-0124 decision 7 still holds: loopback peer only, `Authorization: Bearer` checked in fixed time, a 16 MiB answer bound, and JSON errors (`404 not_found`, `413 too_large`).

## Acceptance criteria

- [x] Each route the ADR names is answered by `ControlRequestHandler`, with the ADR's status codes and body shapes.
- [x] Tests in `Application/Surl.Control.UnitTests` drive each route against a test `IControlTarget` and check the exact JSON or bytes answered, plus `401` without the token and `404 not_found` for a missing item.
- [x] Tests in `Application/Surl.Composition.UnitTests` show `ComposedControlTarget` returns what `ComposedStores` seeded, for one mail message, one file and one queued message.
- [x] A test shows a read publishes no event on the stream the endpoint serves.
- [x] `dotnet build Application/Surl.Control.UnitLibrary -warnaserror` and `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` are clean, and the fast tests pass.

## Notes

- Control types (`ControlMailbox`, `ControlFileContents`, ...) are Control-owned records, as `ControlMessage` is, since Control cannot reference Composition. Flags cross as IMAP names. New `ControlRefusalKind.TooLarge` (add-only) maps to `413 too_large`. Folder listing is `GET /v1/files/{path}/` with a trailing slash per ADR-0164; root is `/v1/files//`. Mail JSON timestamps use the event writer's round-trip format. Event-stream silence is proven in `ComposedControlTargetReadTests`.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Control endpoint serves GET read-back routes for mail, files, queues, retained messages and topic logs over ComposedControlTarget
