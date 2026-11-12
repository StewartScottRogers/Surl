---
id: BL-968
title: Let Surl.Mcp's get_events name and write the consumer_group_changed event
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-946, BL-1035]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-968 — Let Surl.Mcp's get_events name and write the consumer_group_changed event

## Goal

The MCP `get_events` tool accepts `consumer_group_changed` in its `kinds` filter and writes a `ConsumerGroupChangedEvent` with its members (`exchange_id`, `scheme`, `group`, `change`, `member_id`, `generation_id`, `account`), and `account` filtering matches its `Account`, as the control endpoint does since BL-946.

## Context

- ADR-0145 decision 10 adds the event; BL-946 added it to `Surl.Protocol.Abstractions` and `Surl.Control`.
- `Application/Surl.Mcp.UnitLibrary/EventToolFilter.cs` (the `kinds` enum in the tool schema) and `RecordedEventWriter.cs` (exchange id, members) do not know it yet; mirror how `StoreChangedEvent` and `LoginJudgedEvent` are handled.
- Found while delivering BL-946, whose `touches` did not include Surl.Mcp.

## Acceptance criteria

- [x] A `GetEventsToolTests` test shows `kinds: ["consumer_group_changed"]` accepted and returning only that kind.
- [x] A `RecordedEventWriterTests` test pins the JSON written for one `ConsumerGroupChangedEvent` with a member and one without.
- [x] An `account` filter matches a `ConsumerGroupChangedEvent` by its `Account`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and Surl.Mcp.UnitLibrary keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 4): the `account` and `scheme` filters run in Surl.Api's `SurlEventFacts`, which did not
  know `ConsumerGroupChangedEvent` (no kind, no exchange id, no own scheme or account), so `touches` gained
  `Application/Surl.Api` and `Application/Surl.Api.UnitTests` (no task in Doing named them). Code written but
  uncommitted (the shift stashes it): `SurlEventKind.ConsumerGroupChanged`; `SurlEventFacts` kind, exchange id,
  own scheme and own account; MCP `kinds` enum and `RecordedEventWriter` members (null member_id, account and
  exchange_id left out); tests in `SurlEventFactsTests`, `GetEventsToolTests`, `RecordedEventWriterTests`.
  Not built: the pinned .NET SDK 10.0.401 is not installed on the machine, so back to Backlog behind BL-1035.

- 2026-11-12 (lane 3): the stash from lane 4 could not be read, so the change was rewritten from the notes above; SDK 10.0.404 now builds. Coverage tooling was not run; each new branch has a test.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so nothing builds; code is written and stashed by the shift
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. get_events and wait_for_event accept consumer_group_changed, write its members, and filter it by account and scheme
