---
id: BL-1495
title: Write Delivered-To in the control endpoint POST /v1/mail
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487, BL-1498, BL-1500]
touches: [Surl.Control.UnitLibrary, Surl.Control.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1495 — Write Delivered-To in the control endpoint POST /v1/mail

## Goal

The loopback control endpoint's `POST /v1/mail?to=` writes `Delivered-To: <to>` ahead of the posted message when the instance's option is on.

## Context

ADR-0053 Amendment 1 decision 6. Files: `Application/Surl.Control.UnitLibrary/ControlStoreRequests.cs` (and `ControlJson.cs` if a response shows the message); tests `ControlStoreRequestsTests.cs`, `ControlReadRequestsTests.cs`. The control endpoint serves non-.NET test suites, so the field must be exact: one line `Delivered-To: <address>` CRLF for the `to` query value as given, ahead of the posted body, no `Return-Path` or `Received`. Off: unchanged. Depends on BL-1500 and BL-1498. The mail read-back responses are unchanged in shape.

## Acceptance criteria

- [x] A test with the option on: `POST /v1/mail?to=a@x` stores a message whose bytes start `Delivered-To: a@x` CRLF then the posted body; read back through the control read request.
- [x] A test with the option off: the stored bytes equal the posted body.
- [x] A recipient the store discards (no account, no catch-all) stores nothing, as before, pinned by a test.
- [x] `dotnet build Application/Surl.Control.UnitLibrary -warnaserror` is clean and `Surl.Control.UnitTests` fast tests pass at 100% line and branch coverage for the change.

## Notes

- Decision: the endpoint cannot see the store option, so `IControlTarget` gained a default-false `WritesDeliveredTo`; the endpoint prepends the line. The composed target answering it is BL-1515 (needs BL-1502). A discarded recipient stays the target store's existing behaviour: the endpoint hands the message over and the store stores nothing, as before.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. POST /v1/mail writes Delivered-To when the target says the option is on
