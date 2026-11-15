---
id: BL-1202
title: Filter Surl.Mcp's get_events and wait_for_event by store and by a store change's name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1200]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1202 — Filter Surl.Mcp's get_events and wait_for_event by store and by a store change's name

## Goal

`Surl.Mcp`'s `get_events` and `wait_for_event` take a `store` and a name filter, so an agent can wait for one file, mailbox or queue change as a `Surl.Api` test can.

## Context

- Decision: the ADR filed by BL-1194 (property names; `store` values in snake_case, as `RecordedEventWriter.cs` already writes `store`). `SurlEventQuery` already has `Store`; BL-1200 adds the name.
- Where: `Application/Surl.Mcp.UnitLibrary/EventToolFilter.cs` builds the `SurlEventQuery` from a call's arguments and holds the shared schema fragment. `GetEventsTool.cs` and `WaitForEventTool.cs` use it.
- This closes a parity gap: `Surl.Api` filters by store today, and `Surl.Mcp` cannot (configuration as data: one model for every host).

## Acceptance criteria

- [x] Both tools' input schemas list `store` (an enum of every `StoreKind` member in snake_case) and the ADR's name property, with descriptions.
- [x] A test in `Application/Surl.Mcp.UnitTests` seeds a file and a mail message, and shows `get_events` with `store: "content"` returns only the file's change.
- [x] A test shows `wait_for_event` with the name of one seeded path returns that change and times out (`{"timed_out": true}`) for a name nothing touched.
- [x] A test shows an unknown `store` value is refused by the schema check with `isError: true`.
- [x] `dotnet build Application/Surl.Mcp.UnitLibrary -warnaserror` is clean and the fast tests pass.

## Notes

- Property names are `store` and `name`, as the control endpoint's query keys. An unknown `store` is refused by the shared registry's schema check as JSON-RPC `-32602` (not an `isError` result), as every schema failure of every tool is; the test pins that.
- The schema's `store` enum is a literal; a test checks it lists every `StoreKind` in snake_case.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. store and name filters on get_events and wait_for_event
