---
id: BL-1513
title: Show a mailbox's use in the control endpoint listing
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1509]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests, Application/Surl.Composition.UnitLibrary]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1513 — Show a mailbox's use in the control endpoint listing

## Goal

The loopback control endpoint's mailbox listing shows a `use` field for a mailbox that has one, so non-.NET test suites can read it.

## Context

ADR-0055 Amendment 1, decision 6. Depends on `ComposedMailboxInfo.Use` (BL-1509). Code: `Application/Surl.Control.UnitLibrary/ControlEndpoint.cs` and its mailbox-listing JSON. The field appears only when a mailbox has a use (added, never repurposed); listings without a use stay byte-identical. If the endpoint accepts mailbox configuration today, its `folders` items take the `<name>[=<use>]` form; if it takes none, record that in Notes.

## Acceptance criteria

- [x] The mailbox listing JSON contains `"use":"sent"` (the use text) for a mailbox with a use.
- [x] A mailbox without a use produces JSON byte-identical to today's.
- [x] Any existing control-endpoint mailbox configuration accepts folders in `<name>[=<use>]` form with the same refusals, or Notes records that it takes none.
- [x] Tests exist and pass, coverage stays 100%, and `dotnet build` of both projects with `-warnaserror` is clean.

## Notes

- Added `Use` (nullable, default null) to `ControlMailbox` and the JSON record; null is omitted by the context's WhenWritingNull, so listings without a use stay byte-identical (existing INBOX test pins it).
- The control endpoint takes no mailbox configuration (reads and seeds messages only), so no `<name>[=<use>]` folders form applies.
- touches widened with Application/Surl.Composition.UnitLibrary (no Doing task touches it): one-line mapper in ComposedControlTarget passes `box.Use`.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Control mailbox listing shows use when a mailbox has one
