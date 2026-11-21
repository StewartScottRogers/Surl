---
id: BL-1551
title: Say in get_events' and wait_for_event's kinds description that exchange_bytes is never kept
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1551 — Say in get_events' and wait_for_event's kinds description that exchange_bytes is never kept

## Goal

An agent reading the `kinds` property of `get_events` and `wait_for_event` learns that `exchange_bytes`, though
in the enum, is never kept, so asking for it finds nothing (and a wait for it times out), and that the bytes
are read with `get_exchanges`.

## Context

- Surly cookbook bug report 2026-11-21, item 3 (first half): "`exchange_bytes` events come back empty".
- Decided: ADR-0143 decision 2 says `ExchangeBytesEvent` is never kept, bytes living on the exchange records;
  `Application/Surl.Api/EventRecordBook.cs:27-30` returns before keeping one. Also decided: ADR-0171 derives
  the `kinds` enum from every exported `SurlEvent` record, so `EventToolFilter.AllKinds`
  (`Application/Surl.Mcp.UnitLibrary/EventToolFilter.cs:76-96`, `typeof(ExchangeBytesEvent)` at :85) offers
  `exchange_bytes`. The two decisions together make the enum offer a kind no call can return, and the
  schema's `"description": "Only events of these kinds"` (`EventToolFilter.cs:34`) does not say so.
- The enum keeps `exchange_bytes` (configuration only grows: an input accepted today stays accepted); only the
  description changes. The control endpoint keeps byte events (ADR-0136) and is not touched.
- Tests: `Application/Surl.Mcp.UnitTests/GetEventsToolTests.cs`, `WaitForEventToolTests.cs`.
- Stand-in: no store, event or fault changes.

## Acceptance criteria

- [ ] The `kinds` description in `EventToolFilter`'s schema says `exchange_bytes` events are never kept, so
  they are never returned or waited for, and that `get_exchanges` shows the bytes.
- [ ] A test asserts both tools' input schemas carry that description.
- [ ] A test asserts `get_events` with `kinds: ["exchange_bytes"]` after an exchange returns no events and is
  not an error, pinning today's behaviour.
- [ ] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean and
  `dotnet test Application/Surl.Mcp.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`'s `GetEventsTool` entry says `exchange_bytes` is in the enum but never kept.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
