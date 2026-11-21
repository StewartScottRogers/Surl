---
id: BL-1517
title: Answer IControlTarget.WritesDeliveredTo from the composed mail store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1495, BL-1502]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-21
---
# BL-1517 — Answer IControlTarget.WritesDeliveredTo from the composed mail store

## Goal

`ComposedControlTarget` answers `IControlTarget.WritesDeliveredTo` with the shared mail store's `DeliveredToEnabled`, so the control endpoint's `POST /v1/mail` writes `Delivered-To` in a composed instance.

## Context

BL-1495 added the default-false `WritesDeliveredTo` member and the endpoint's prepend; BL-1502 sets `DeliveredToEnabled` on the store. `Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs` must expose it (via `ComposedStores`).

## Acceptance criteria

- [x] A composed instance with `--mail-delivered-to` answers true and one without answers false, pinned by tests.
- [x] A composed-instance test: `POST /v1/mail?to=` stores bytes starting `Delivered-To: <to>` CRLF when on.
- [x] Build clean and Composition fast tests pass at 100% coverage for the change.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. ComposedControlTarget.WritesDeliveredTo answers the mail store's DeliveredToEnabled; tests pin both settings and POST /v1/mail
