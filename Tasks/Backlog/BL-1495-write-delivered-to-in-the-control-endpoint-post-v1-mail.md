---
id: BL-1495
title: Write Delivered-To in the control endpoint POST /v1/mail
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487, BL-1488, BL-1490]
touches: [Surl.Control.UnitLibrary, Surl.Control.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1495 — Write Delivered-To in the control endpoint POST /v1/mail

## Goal

The loopback control endpoint's `POST /v1/mail?to=` writes `Delivered-To: <to>` ahead of the posted message when the instance's option is on.

## Context

ADR-0053 Amendment 1 decision 6. Files: `Application/Surl.Control.UnitLibrary/ControlStoreRequests.cs` (and `ControlJson.cs` if a response shows the message); tests `ControlStoreRequestsTests.cs`, `ControlReadRequestsTests.cs`. The control endpoint serves non-.NET test suites, so the field must be exact: one line `Delivered-To: <address>` CRLF for the `to` query value as given, ahead of the posted body, no `Return-Path` or `Received`. Off: unchanged. Depends on BL-1490 and BL-1488. The mail read-back responses are unchanged in shape.

## Acceptance criteria

- [ ] A test with the option on: `POST /v1/mail?to=a@x` stores a message whose bytes start `Delivered-To: a@x` CRLF then the posted body; read back through the control read request.
- [ ] A test with the option off: the stored bytes equal the posted body.
- [ ] A recipient the store discards (no account, no catch-all) stores nothing, as before, pinned by a test.
- [ ] `dotnet build Application/Surl.Control.UnitLibrary -warnaserror` is clean and `Surl.Control.UnitTests` fast tests pass at 100% line and branch coverage for the change.

## Notes

## Log

- 2026-11-20: Created.
