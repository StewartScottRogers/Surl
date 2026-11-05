---
id: BL-830
title: Answer a chosen protocol error by fault in the extended CoAP, MCP and SignalR servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-062
created: 2026-11-05
completed:
---
# BL-830 — Answer a chosen protocol error by fault in the extended CoAP, MCP and SignalR servers

## Goal

An `error` fault rule naming `coap`, `coaps`, `mcp`, `mcps`, `signalr` or `signalrs` (or a listener of one) makes
that server answer the matching request with the chosen protocol error, instead of being refused at configuration.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4
  and 9 and its extended reply table: CoAP a response code 4.00 to 5.05; MCP a JSON-RPC error code and message
  (`command` the JSON-RPC method or tool name); SignalR text sent as the invocation's completion `error`
  (`command` the hub method). `path` is the URI path (CoAP) or the listen path.
- Each server is judged by its own specification (ADR-0083); unit tests replay bytes written from it. Consult
  `ExchangeContext.Faults` per request after login; publish `FaultAppliedEvent` (BL-823's contracts and doubles).
  `Surl.McpServing` is shared with `Surl.Mcp` and not touched: the fault is consulted in the server.
- `Surl.Cli`: add these schemes' reply shapes to BL-785's `--fault` reply check.

## Acceptance criteria

- [ ] Named tests per server: a request matching an `error` rule gets exactly the chosen error, a non-matching one
      is served, and one `FaultAppliedEvent` is published.
- [ ] Named `Surl.Cli` tests: one accepted and one refused `reply` per scheme family.
- [ ] With no rule, every existing test of the touched libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Filed by BL-822 (ADR-0124 decision 9); depends on BL-785 and touches `Surl.Cli` for the reply shapes.
