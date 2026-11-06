---
id: BL-835
title: Answer a chosen protocol error by fault in the extended CoAP, MCP and SignalR servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitTests, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0144-how-the-coap-mcp-and-signalr-servers-answer-a-chosen-error.md, Documentation/Planning/Decisions/README.md]
requirement: FR-062
created: 2026-11-05
completed: 2026-11-06
---
# BL-835 — Answer a chosen protocol error by fault in the extended CoAP, MCP and SignalR servers

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

- [x] Named tests per server: a request matching an `error` rule gets exactly the chosen error, a non-matching one
      is served, and one `FaultAppliedEvent` is published.
- [x] Named `Surl.Cli` tests: one accepted and one refused `reply` per scheme family.
- [x] With no rule, every existing test of the touched libraries passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and
      branch coverage.

## Notes

Delivered (2026-11-06, dark factory lane 4) under ADR-0144 (decided by Claude under Stewart's delegation; 0141, 0142 and
0143 were taken). No upstream curl measurement: these are extended schemes, judged by their specifications
(ADR-0083), so every test replays bytes written from RFC 7252, JSON-RPC 2.0 and HubProtocol.md.

- CoAP (`CoapRequestFaults`, wired in `CoapFlowServing`): asked per request before observers and resources;
  command the method (`GET`, other codes as `0.dd`), path the Uri-Path; reply `c.dd` 4.00-4.31 or 5.00-5.05 with
  an optional diagnostic text sent as the payload. No account: a PSK identity names none.
- MCP (`McpRequestFaults`, in `McpHttpRequestAnswerer.AnswerInSessionAsync`): asked per in-session request after
  every check; command the tool name for `tools/call`, else the method; path the endpoint; account the login
  (the empty login of an unnamed bearer is none). Reply `<integer> <message>`, written by the core's
  `JsonRpcResponseWriter.WriteError`. `initialize` is not offered (refusing a session is `refuse-login`'s);
  `Surl.McpServing` untouched.
- SignalR (`SignalRInvocationFaults`, given to `SignalRHubSession` by `SignalRWebSocketTakeover`): asked per
  invocation with an `invocationId`; command the target, path the endpoint base, account the negotiator. A
  non-blocking Invocation has no Completion to carry an error, so it is never offered.
- `Surl.Cli` `FaultReplyShapes` gained `coap`, `coaps`, `mcp`, `mcps`, `signalr`, `signalrs`, with the same
  shapes. The listen URL is formatted with ADR-0141's shared `ReportedListenUrl.Format`, not a copy.
- Delay and drop at these points are filed as **BL-943**.
- `touches` grew by ADR-0144 and the decisions README; no task in Doing names them.

Tests: `CoapRequestFaultsTests`, `McpRequestFaultsTests`, `SignalRInvocationFaultsTests`, new rows in
`CommandLineParserFaultTests` (`Parse_ErrorReplyFittingItsScheme_IsServed`,
`Parse_FaultRefused_IsRefusedWithItsReason`). CoAP 316, MCP 294, SignalR 224, Cli 1199 tests; all 78 fast test
assemblies green; `dotnet build -warnaserror` clean. CoAP, MCP and SignalR libraries at 100% line and branch
(Cobertura from the MSTest collector); `Surl.Cli` at 100% line, and `FaultReplyShapes` at 100% branch - its
five partial branches (`CommandLineParser`, `ConfigurationItemReader`, `HelpText`, `MailboxFileReader`,
`OptionArgumentReader`) are in files this task did not change.
## Log

- 2026-11-05: Created.
- 2026-11-05: Filed by BL-822 (ADR-0124 decision 9); depends on BL-785 and touches `Surl.Cli` for the reply shapes.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. CoAP, MCP and SignalR answer a matching chosen-error fault rule (response code, JSON-RPC error, Completion error) and publish FaultAppliedEvent; surl --fault checks their reply shapes
