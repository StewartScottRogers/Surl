---
id: BL-805
title: Expose each composed store's seed, reset, snapshot and restore through ComposedServing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-803, BL-799, BL-800, BL-801, BL-802]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-805 — Expose each composed store's seed, reset, snapshot and restore through ComposedServing

## Goal

A host holding a `ComposedServing` can seed, reset, snapshot and restore that instance's mail store, content
store, message bus and account set - one store at a time or all together - so `Surl.Api`, `Surl.Mcp` and the
control endpoint all drive the same code.

## Context

- Decision: BL-822's ADR decision 3 (the whole-instance snapshot - planning name `ComposedSnapshot` holding one
  snapshot per store - and reset's order across stores; what happens with `--directory`).
- Built on: BL-799 (mail), BL-800 (content), BL-801 (bus), BL-802 (accounts), BL-803 (the instance's stream).
- Where: `Application/Surl.Composition.UnitLibrary` (`ComposedServing.cs`, `ServiceState.cs`). Expose a
  store-control surface (planning name `ComposedStores`) on `ComposedServing`; keep these types `public` but
  outside `Surl.Api`'s supported surface, as the library's `CLAUDE.md` says. Update that `CLAUDE.md`.
- Reset and restore of all stores is one operation per store in the fixed load order of ADR-0100 decision 1
  (content, mail, bus, then accounts as the ADR places them), so a failure is reported deterministically.
- Stand-in: controllable; the cross-protocol proof is in Acceptance.

## Acceptance criteria

- [ ] Named tests over `FakeListenerFactory` (no network): a message seeded through `ComposedServing` is fetched
      over a fake IMAP connection; a file seeded is read over a fake HTTP connection; reset empties both and keeps
      the configured mailboxes and shares; snapshot, change, restore of the whole instance gives back every store's
      state; each operation's events appear on the instance's stream.
- [ ] A named test shows the `--directory` behaviour the ADR decides.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
