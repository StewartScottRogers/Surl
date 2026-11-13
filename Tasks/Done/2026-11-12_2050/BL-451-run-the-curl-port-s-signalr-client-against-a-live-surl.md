---
id: BL-451
title: Run the Curl port's signalr client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-450, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-451 — Run the Curl port's signalr client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl signalr://` and `signalrs://`: an echo
invocation, a failing invocation, and a refused unauthenticated negotiate. Each assertion comes from
HubProtocol.md and TransportProtocols.md, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help signalr` and its cases in `Curl.Conformance.Extended.UnitTests/SignalR/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [x] The test class exists with the three exchanges, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/SignalR.md`: every row is covered by a named test or marked `n/a` with a
      reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- Live tests: `CurlPortClientInvokesHubMethodsOnSurlOverSignalRTests` (echo over `signalr`, echo over `signalrs`, `Fail`, negotiate refused). All four pass on Windows with the pinned client. The matrix rows they prove name them; every row was already covered by a named unit test or `n/a` with a reason, and `SignalR:Surl-0-1` now also names the TLS echo.
- Port defect (reported, not asserted): the pinned client exits 0 with no output when negotiate is answered `401`. The refusal is therefore asserted on the wire with a raw `POST /negotiate`, and on the client printing no result.
- Server defect found: with no account and no `--allow-anonymous`, negotiate is answered `200` (`SignalRRequestAnswerer` passes `IsWrite: false`; `HttpAuthenticationSession` lets reads through when no account exists). Filed as BL-1091; the refusal test here uses `--user` so it pins what works today.

## Log

- 2026-11-04: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Pinned Curl port signalr client passes echo over signalr and signalrs, a failing Fail invocation and a refused negotiate against live surl; matrix names the tests
