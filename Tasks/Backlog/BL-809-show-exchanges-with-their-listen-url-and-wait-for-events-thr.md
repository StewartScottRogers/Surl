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
completed:
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

- [ ] Named tests: `get_exchanges` returns `listen_url` and `scheme` per exchange; each filter the ADR defines
      returns only matching exchanges; the event tool returns events of the kinds the ADR names; `wait_for_event`
      returns a matching event and returns the ADR's timeout result on a fake `TimeProvider`; an out-of-range
      timeout and an unknown server ID are `isError: true`.
- [ ] A named test shows `tools/list` from `RunAsync` lists every new tool with a JSON object input schema and a
      description.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` and `Surl.Mcp`
      keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
