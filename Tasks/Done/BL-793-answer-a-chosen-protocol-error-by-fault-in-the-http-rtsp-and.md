---
id: BL-793
title: Answer a chosen protocol error by fault in the HTTP, RTSP and WebSocket servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests, Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
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

- [x] Named tests per server, driving it with `InMemoryConnection` replaying upstream curl 8.21.0's request bytes:
      HTTP `GET /a` with a rule `status 503` on path `/a` sends exactly the ADR's 503 answer while `GET /b` is
      served; RTSP `DESCRIBE` with a rule answers the chosen status; a WebSocket upgrade with a rule is refused (or
      closed) as the ADR says; each publishes one `FaultAppliedEvent`.
- [x] A named test per server shows a rule scoped to account `alice` fires only after `alice` logged in.
- [x] With no rule, every existing test of the three libraries passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the three libraries keep 100% line and
      branch coverage.

## Notes

- **Plan and decisions** (ADR-0134, decided by Claude under Stewart's delegation): one shared helper,
  `Surl.HttpMessage`'s `HttpCommandFaults`, builds the `Command` point (listen URL formatted as the engine
  formats it), returns only `Error` rules, reads the reply and publishes `FaultAppliedEvent`. HTTP asks in
  `HttpRequestAnswerer` (so HTTP/1.x, HTTP/2 and HTTP/3 all get it - no follow-up per version needed) after
  the login, before the upload limit and the content store; a body-bound `PUT` only once its body check
  accepts. RTSP asks after the login verdict, before the method's checks. WebSocket asks after the login and
  field checks and answers `101` then a `CLOSE` with the rule's code and reason.
- **Touches widened**: `Surl.HttpMessage.UnitLibrary` and `.UnitTests` added, because all three servers
  reference it and the helper would otherwise be written three times; no task in Doing names either.
- **Measured with pinned upstream curl 8.21.0** (`Record-CurlExchange.ps1`, fixtures in each test project):
  HTTP `GET /a` answered `503` (keep-alive, empty body) exits 0 and curl sends `GET /b` on the same
  connection; with `--fail` exit 22 (`The requested URL returned error: 503`). libcurl `DESCRIBE` answered
  `RTSP/1.0 503` ends `CURLcode 0`, status 503; the `curl` tool's `OPTIONS` with `-f` exits 22. WebSocket
  `101` then `CLOSE 1013 try later` exits 0, the close payload on stdout; `CLOSE 1011` alone exits 0. An empty
  reason phrase (`HTTP/1.1 599 `, `RTSP/1.0 599 `) exits 0.
- **Defaults taken**: a reply with no reason phrase gets RFC 9110's (HTTP) or RFC 2326's (RTSP) phrase, else
  an empty one; a reply that does not fit is noted and not applied (no event); a WebSocket close code
  outside what RFC 6455 allows on the wire (1004, 1016-2999) is not sent; `delay`/`drop` at the command stage
  are noted and not applied here.
- **Follow-ups filed**: BL-894 (delay and drop at the command stage in these servers), BL-895 (Surl.Cli
  refuses reserved WebSocket close codes).
- **Coverage**: `Measure-CodeQuality.ps1` over the four test projects: HttpMessage, Http, Rtsp, Ws all 100% line
  and 100% branch; no new member over the thresholds (`FileAnswer` and `StoreUploadAsync` in Http were already
  flagged on the coverage-derived complexity column and are unchanged).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. A chosen-error fault rule now answers HTTP (all versions) and RTSP with the rule's status and WebSocket with 101 then the rule's CLOSE, after login, publishing one FaultAppliedEvent (ADR-0134)
