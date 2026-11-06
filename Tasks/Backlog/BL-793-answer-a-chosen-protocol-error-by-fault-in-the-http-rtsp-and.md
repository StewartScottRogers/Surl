---
id: BL-793
title: Answer a chosen protocol error by fault in the HTTP, RTSP and WebSocket servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests, Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-793 — Answer a chosen protocol error by fault in the HTTP, RTSP and WebSocket servers

## Goal

When a chosen-error fault rule matches a request (by listener or scheme, account, method and path), the HTTP
server answers it with the rule's status (for example `503 Service Unavailable`), RTSP with the rule's status, and
the WebSocket server refuses the upgrade or closes with the rule's close code, each in its protocol's own framing,
so a test can make one request fail on purpose.

## Context

- Decision: BL-822's ADR decision 4 (the chosen-error kind: what "command" means per protocol, whether the reply
  is checked against the protocol's grammar, the reason phrase and body, `Connection: close` or not).
- Contract: BL-823 (`ExchangeContext.Faults`, `FaultStage` for a command, `FaultAppliedEvent` through
  `ExchangeContext.Events`). Tests use BL-823's scripted fault plan and recording publisher; no engine needed.
- Where: `Surl.Protocol.Http` (HTTP/1.1, and HTTP/2 if it is answered there), `Surl.Protocol.Rtsp`,
  `Surl.Protocol.Ws`. Consult the plan once per request after the request head is read and authentication has
  run (a fault never skips a login check), before the content store is touched. HTTP/3 is moving to its own
  library (BL-410): if HTTP/2 or HTTP/3 answering lives outside these libraries when this runs, file one
  follow-up task per library rather than widening this one.
- Request bytes in tests are measured from pinned upstream curl 8.21.0 (`Record-CurlExchange.ps1`), never the
  Curl port (ADR-0003). Record in Notes the exit code upstream curl 8.21.0 gives for each named case (e.g. `-f`
  and a 503 gives 22).
- Stand-in: no store change; the fault is visible to tests as a `FaultAppliedEvent` and the exchange's bytes.

## Acceptance criteria

- [ ] Named tests per server, driving it with `InMemoryConnection` replaying upstream curl 8.21.0's request bytes:
      HTTP `GET /a` with a rule `status 503` on path `/a` sends exactly the ADR's 503 answer while `GET /b` is
      served; RTSP `DESCRIBE` with a rule answers the chosen status; a WebSocket upgrade with a rule is refused (or
      closed) as the ADR says; each publishes one `FaultAppliedEvent`.
- [ ] A named test per server shows a rule scoped to account `alice` fires only after `alice` logged in.
- [ ] With no rule, every existing test of the three libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the three libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
