---
id: BL-626
title: Show AI agents what a client sent through a Surl.Mcp exchanges tool
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-617, BL-625]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-626 — Show AI agents what a client sent through a Surl.Mcp exchanges tool

## Goal

An agent can call one `Surl.Mcp` tool (planning name `get_exchanges`) with a server's ID and get every exchange that
server has served - listen URL, client address, start time, and the bytes received and sent in order - with bytes
shown as text where they are UTF-8 and as base64 where not, cut to the ADR's cap, so it can check exactly what its
client sent.

## Context

- Decision: BL-612's ADR, decision 3 (the tool's name, schema, result shape, the text-or-base64 rule and the cap,
  and any filter such as "since exchange N"). Servers the `start_server` tool starts turn on `Surl.Api`'s exchange
  record (BL-617, BL-611's ADR decision 7).
- The tool's logic goes in `Surl.Mcp.UnitLibrary` (BL-624); `Surl.Mcp` (BL-625) registers it with one line.
- The port's `McpReturnedBytes` (text or base64 cut to a cap, ADR-0555 of the port) is copyable code
  (`git -C Z:\repos\Curl show origin/master:Curl.Mcp.UnitLibrary/CLAUDE.md`); it is not evidence.

## Acceptance criteria

- [x] Named tests, with the fake listener seam and a fake connection writing known bytes, show: a server's exchange
      returned with its listen URL, client address and the exact bytes received and sent; non-UTF-8 bytes returned as
      base64 and marked so; bytes past the cap cut and the cut stated as the ADR says; an unknown server ID an
      `isError` result; two exchanges returned in the order they started.
- [x] A named test in `Surl.Mcp.UnitTests` shows `tools/list` from `RunAsync` now includes the tool, with a JSON
      object input schema and a description.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` and `Surl.Mcp` keep
      100% line and branch coverage.

## Notes

- Built to ADR-0103 decision 3 as written: `GetExchangesTool` (`get_exchanges`, registered fourth in
  `Program.RunAsync`) and `RecordedExchangeWriter` (text-or-base64 per chunk, 64 KiB per exchange, 1 MiB per
  result, `after_exchange_id`, `max_exchanges` default 20). No listen URL per exchange: the ADR's departure for
  BL-626 drops it, because `Surl.Api` records none (ADR-0099 decision 7); the tests check the client address instead.
- Choices within the ADR (sensible defaults, no new ADR needed): the UTF-8 back-off applies only to a chunk shown
  as text, so a base64 chunk is cut at exactly the cap; a text chunk that backs off to nothing is left out rather
  than written empty; `has_more` is also true when `max_exchanges` stops the list, so the agent knows to ask again
  with `after_exchange_id`.
- The "exact bytes" test runs a real HTTP GET (upstream curl 8.21.0's request head, as `Surl.Api`'s tests pin it)
  through `FakeListenerFactory` and `FakeConnection`. That fake accepts one connection only, so the "two exchanges
  in start order" test feeds two `SurlExchangeRecord`s to `GetExchangesTool.Answer` directly.
- `initialize`'s `instructions` and `--help` now also say the tools show what clients sent.
- Coverage: `Surl.Mcp.UnitLibrary` and `surl-mcp` both at 100% line and branch (Surl.Mcp.UnitTests, 138 tests).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl-mcp's get_exchanges tool shows each exchange's client, start time and bytes received and sent, as text or base64, cut to 64 KiB per exchange and 1 MiB per result
