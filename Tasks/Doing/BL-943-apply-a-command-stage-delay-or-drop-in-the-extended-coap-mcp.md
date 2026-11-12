---
id: BL-943
title: Apply a command-stage delay or drop in the extended CoAP, MCP and SignalR servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-835, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests]
requirement: FR-062
created: 2026-11-06
completed:
---
# BL-943 — Apply a command-stage delay or drop in the extended CoAP, MCP and SignalR servers

## Goal

A `delay` or `drop` rule at `stage=command` matching a CoAP request, an MCP request in a session or a SignalR
invocation is applied there, instead of being noted as not applied.

## Context

- BL-835 (ADR-0144) offers these points to the fault plan for `error` rules only; any other kind is noted
  "not applied" by `CoapRequestFaults`, `McpRequestFaults` and `SignalRInvocationFaults`.
- ADR-0141 decides how a server applies a command-stage delay or drop: `CommandStageFaults.FindAndApplyAsync`
  in `Surl.Protocol.Abstractions` (delay on the exchange's clock with the idle clock paused; drop aborts the
  connection). For CoAP, a datagram flow, "drop" means sending no answer to that request; decide and record it.

## Acceptance criteria

- [ ] Named tests per server: a command-stage `delay` rule holds the answer for its `ms` on the injected clock,
      a `drop` rule ends the connection (CoAP: sends nothing for that request), and one `FaultAppliedEvent` is
      published for each.
- [ ] With no rule, every existing test of the touched libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line
      and branch coverage.

## Notes

2026-11-11 (lane 3): `dotnet build` cannot run - global.json pins SDK 10.0.401 and only 10.0.100-rc.1,
11.0.100-rc.2 and 11.0.100 are installed. Added BL-1035 (Stewart reinstalls the SDK) to depends-on and
returned the task to Backlog. Design worked out before stopping, for the next run:
- CoAP: the answer delegate returns `CoapResponse?`; null (a drop) sends nothing and is remembered in the
  duplicate cache with no bytes, so retransmissions of that CON also get nothing. `CommandStageFaults` needs an
  `IConnection`, which a datagram flow is not, so CoAP holds a delay itself, the same way: note, one event,
  idle clock paused, `Task.Delay` on the context's clock. A CON delay past `SeparateResponseAfter` gets the
  Empty ACK and a separate response, as any slow answer does. Record it as an amendment to ADR-0144.
- SignalR: `SignalRHubSession` holds the connection, so call `CommandStageFaults.FindAndApplyAsync` there; on
  a drop end the session without a Completion.
- MCP: `McpProtocolServer.ServeAsync` can hand the connection to `McpHttpRequestAnswerer`, but after
  `Abort()` `Http1AnswerWriter` writes the returned answer to an aborted connection and throws `IOException`;
  `HttpAnswer` has no "write nothing" (BL-902 adds one in `Surl.HttpServing`). Either wait for BL-902's seam or
  throw a recognised drop end from the answerer; decide in the amendment.

## Log

- 2026-11-06: Created.
- 2026-11-06: Filed by BL-835 (ADR-0144), which applies only the `error` kind at these points.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-12: Backlog -> Doing.
