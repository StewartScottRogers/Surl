---
id: BL-1513
title: Show a mailbox's use in the control endpoint listing
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1509]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1513 — Show a mailbox's use in the control endpoint listing

## Goal

The loopback control endpoint's mailbox listing shows a `use` field for a mailbox that has one, so non-.NET test suites can read it.

## Context

ADR-0055 Amendment 1, decision 6. Depends on `ComposedMailboxInfo.Use` (BL-1509). Code: `Application/Surl.Control.UnitLibrary/ControlEndpoint.cs` and its mailbox-listing JSON. The field appears only when a mailbox has a use (added, never repurposed); listings without a use stay byte-identical. If the endpoint accepts mailbox configuration today, its `folders` items take the `<name>[=<use>]` form; if it takes none, record that in Notes.

## Acceptance criteria

- [ ] The mailbox listing JSON contains `"use":"sent"` (the use text) for a mailbox with a use.
- [ ] A mailbox without a use produces JSON byte-identical to today's.
- [ ] Any existing control-endpoint mailbox configuration accepts folders in `<name>[=<use>]` form with the same refusals, or Notes records that it takes none.
- [ ] Tests exist and pass, coverage stays 100%, and `dotnet build` of both projects with `-warnaserror` is clean.

## Notes

## Log

- 2026-11-20: Created.
