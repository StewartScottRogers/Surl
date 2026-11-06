---
id: BL-968
title: Let Surl.Mcp's get_events name and write the consumer_group_changed event
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-946]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-968 — Let Surl.Mcp's get_events name and write the consumer_group_changed event

## Goal

The MCP `get_events` tool accepts `consumer_group_changed` in its `kinds` filter and writes a `ConsumerGroupChangedEvent` with its members (`exchange_id`, `scheme`, `group`, `change`, `member_id`, `generation_id`, `account`), and `account` filtering matches its `Account`, as the control endpoint does since BL-946.

## Context

- ADR-0145 decision 10 adds the event; BL-946 added it to `Surl.Protocol.Abstractions` and `Surl.Control`.
- `Application/Surl.Mcp.UnitLibrary/EventToolFilter.cs` (the `kinds` enum in the tool schema) and `RecordedEventWriter.cs` (exchange id, members) do not know it yet; mirror how `StoreChangedEvent` and `LoginJudgedEvent` are handled.
- Found while delivering BL-946, whose `touches` did not include Surl.Mcp.

## Acceptance criteria

- [ ] A `GetEventsToolTests` test shows `kinds: ["consumer_group_changed"]` accepted and returning only that kind.
- [ ] A `RecordedEventWriterTests` test pins the JSON written for one `ConsumerGroupChangedEvent` with a member and one without.
- [ ] An `account` filter matches a `ConsumerGroupChangedEvent` by its `Account`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and Surl.Mcp.UnitLibrary keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
