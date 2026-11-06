---
id: BL-809
title: Show exchanges with their listen URL and wait for events through Surl.Mcp's tools
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-807]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-809 — Show exchanges with their listen URL and wait for events through Surl.Mcp's tools

## Goal

An agent driving `surl-mcp` sees each exchange's listen URL and scheme in `get_exchanges`, can filter exchanges
and events by scheme, listen URL, account and time, and can wait (bounded) for the first matching event, through
the same `Surl.Api` queries a .NET test uses.

## Context

- Follow-up of BL-626 (Done): `get_exchanges` (`GetExchangesTool`, `RecordedExchangeWriter` in
  `Application/Surl.Mcp.UnitLibrary`) shows no listen URL, because `Surl.Api` recorded none (ADR-0103 decision 3).
  BL-806 adds it; BL-807 adds the queries and waits.
- Decision: BL-822's ADR decision 8 (amends ADR-0103 decision 3: the new properties on `get_exchanges`, the new
  tool or tools - planning `get_events` and `wait_for_event` - their schemas, snake_case names, and bounds; the wait
  capped, planning 1 to 60000 ms).
- Where: tool logic in `Application/Surl.Mcp.UnitLibrary`; one registration line per new tool in
  `Application/Surl.Mcp` (`Program.RunAsync`). Results follow ADR-0103 decision 3's rules: `structuredContent` and
  the same object as the one text block; an unknown server ID is `isError: true`; bytes keep the text-or-base64
  rule and caps. `initialize`'s `instructions` and `--help` mention the new tools.
- Stand-in: subscriber of the event stream, for agents.

## Acceptance criteria

- [x] Named tests: `get_exchanges` returns `listen_url` and `scheme` per exchange; each filter the ADR defines
      returns only matching exchanges; the event tool returns events of the kinds the ADR names; `wait_for_event`
      returns a matching event and returns the ADR's timeout result on a fake `TimeProvider`; an out-of-range
      timeout and an unknown server ID are `isError: true`.
- [x] A named test shows `tools/list` from `RunAsync` lists every new tool with a JSON object input schema and a
      description.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` and `Surl.Mcp`
      keep 100% line and branch coverage.

## Notes

- Delivered: `get_exchanges` writes `listen_url` and `scheme` per exchange and takes optional `scheme` and
  `listen_url` filters, found with `SurlServer.FindExchanges`; new `get_events` (`SurlServer.FindEvents`) and
  `wait_for_event` (`SurlServer.WaitForEventAsync`), registered after `get_exchanges` in `Program.RunAsync`.
  `initialize`'s `instructions` and `--help` name what they do. `EventToolFilter` turns the arguments into the
  `SurlEventQuery` a .NET test would pass; `RecordedEventWriter` writes events as the control endpoint does
  (ADR-0124 decision 7): `sequence`, `at`, `kind`, then members and enum values in snake_case, nulls left out.
- Filters are exactly ADR-0124 decision 8's. "Time" in the Goal is served by `after_sequence` and each event's
  `at`; `since`/`until` are not in the ADR's schema, so they were not added.
- Choice: `timeout_ms` is range-checked by the tool, not by the schema (`minimum`/`maximum`), because a schema
  violation is a JSON-RPC `-32602` and the acceptance criteria ask for `isError: true`. `max_events` keeps its
  range in the schema, as `max_exchanges` does.
- Choice: `kinds` may name several kinds while a `SurlEventQuery` names one, so `wait_for_event` waits with no
  kind and, on an event of a kind not asked for, moves `AfterSequence` past it and waits again; one
  `CancellationTokenSource` on the host's `TimeProvider` bounds the whole loop. A caller's cancellation still
  propagates; only the deadline becomes `{"timed_out": true}`.
- Choice: results carry exactly the ADR's members (no `server_id` on the event tools' results).
- The event tests drive a GET with `-u alice:secret --allow-plaintext-auth` and an `Authorization: Basic` header
  in upstream curl's header order, so a `login_judged` event exists for the `account` filter.
- Tests: `Surl.Mcp.UnitTests` 190 (new `GetEventsToolTests`, `WaitForEventToolTests`, `RecordedEventWriterTests`;
  more in `GetExchangesToolTests` and `ProgramTests`). `ManualTimeProvider` is linked in from
  `Surl.Api.UnitTests` for the timeout tests. Coverage from those tests: `Surl.Mcp.UnitLibrary` 100% line and
  branch (0 failing members, worst CRAP 10), `surl-mcp` 100% line and branch.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. surl-mcp's get_exchanges shows each exchange's listen URL and scheme and filters by them; get_events and wait_for_event query and wait for events through Surl.Api
