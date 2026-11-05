---
id: BL-449
title: Serve SignalR hub invocations over the WebSocket transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-448, BL-408]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-449 — Serve SignalR hub invocations over the WebSocket transport

## Goal

The SignalR server upgrades `<hub>?id=<connectionToken>` to WebSocket and completes the hub-protocol handshake.
It answers invocations with completions, stream invocations with stream items, and pings and closes. It sends
keep-alive pings, and closes a connection whose client went quiet past the timeout.

## Context

- Decision: BL-447's ADR, decisions 1, 3, 4, 6 and 7.
- The WebSocket handshake and frames: BL-408's library (add its reference).
- HubProtocol.md: "Overview" (handshake first, then messages), "Invocation", "StreamInvocation", "CancelInvocation",
  "Completion" (result or error), "Ping", "Close" (`error`, `allowReconnect`), and the non-blocking invocation
  (no `invocationId`, so no completion).
- Time through an injected `TimeProvider`. Tests use the test project's `ManualTimeProvider`.

## Acceptance criteria

- [x] Named tests, through an in-memory connection with WebSocket frames written from RFC 6455 and hub messages
      from HubProtocol.md, show:
      - the handshake answered with `{}`;
      - an unsupported protocol answered with the handshake error;
      - an echo invocation completed with its result;
      - a non-blocking invocation getting no completion;
      - a failing method completed with `error`;
      - a stream invocation sending items then a completion;
      - a CancelInvocation stopping the stream;
      - a ping every keep-alive interval;
      - a quiet client closed with a `Close` message after the timeout;
      - a connection token not from negotiate refused.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Built: `SignalRHubConnections.TakeUp`/`End` with `SignalRUpgradeOutcome` (the upgrade's `id`: `101`, `404`,
  `409`, `403`, `503`); the upgrade answer in `SignalRRequestAnswerer` (`Surl.WebSocket`'s checks, then the `101`
  with a `SignalRWebSocketTakeover`); `SignalRHubSession` (read loop, clock, `Count` streams, one writer at a
  time, `SignalRSessionEnd`); `SignalRHubMethods`/`SignalRHubCall` (`Echo`, `Count`, `Fail`, the fixed errors).
- Tests: `SignalRHubSessionTests` drives the full server through `UnitTestDuplexConnection` (a connection the test
  feeds while the server runs) with frames written from RFC 6455 and records from HubProtocol.md, on a
  `ManualTimeProvider` whose `AdvanceWhenWaitingAsync` waits for the server's timer before moving the clock, so
  the keep-alive, timeout and `Count` interval tests are deterministic (repeated 5 times, stable). Acceptance
  bullets map to: `RunAsync_Handshake_IsAnsweredWithAnEmptyObject`,
  `RunAsync_HandshakeRefused_AnswersTheHandshakeErrorThenClosesNormally`,
  `RunAsync_EchoInvocation_CompletesWithItsArgumentByteForByte`, `RunAsync_NonBlockingInvocation_GetsNoCompletion`,
  `RunAsync_FailingMethod_CompletesWithError`,
  `RunAsync_StreamInvocation_SendsEachItemAHundredMillisecondsApartThenACompletion`,
  `RunAsync_CancelInvocation_StopsTheStreamAndCompletesIt`,
  `RunAsync_ServerSendsNothingForAKeepAliveInterval_SendsAPingEachInterval`,
  `RunAsync_ClientQuietPastTheTimeout_IsClosedWithTheTimeoutClose`, and
  `SignalRRequestAnswererTests.AnswerAsync_UpgradeWithAConnectionTokenNotFromNegotiate_IsNotFound`.
- Measured with `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.SignalR.UnitLibrary`: 100% line, 100%
  branch, 0 failing members, worst CRAP 10. Two BL-448 methods (`HubHandshake.JudgeRoot`,
  `HubMessageJsonReader.Build`) measured complexity 14 and 12; each was split to stay at or under 10.
- Scope (sensible default): the acceptance criteria are what "done" means, so ADR-0119's `Broadcast`, `Collect`
  uploads, the shutdown farewell (`{"type":7}` then `1001`) and the 1048576-byte unsent cap went to BL-772 rather
  than widening this task. Until then `Broadcast` and `Collect` answer `Unknown hub method.`, a client StreamItem
  or Completion is a protocol error, and a cancelled exchange ends with nothing written.
- Choice: a client WebSocket Close is answered with Close `1000`, not an echo of its code: ADR-0119 decisions 3
  and 4 end the hub's normal closes with `1000`, and RFC 6455 section 5.5.1 only says an endpoint "typically"
  echoes the code. A GET without an upgrade now gets `Surl.WebSocket`'s full `426` refusal (`Upgrade: websocket`
  and `Sec-WebSocket-Version: 13`), as ADR-0119 decision 2 names that refusal; the BL-448 test was updated.
- `touches` widened to `Documentation/Conformance/Extended/SignalR.md` (no task in Doing names it) because
  ADR-0119's Consequences have each task replace its ID in that matrix with the named test; every `BL-449` row now
  names its tests, and the rows left for BL-772 point at it. ADR-0119 itself was not edited: a task in Doing holds
  `Documentation/Planning/Decisions`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. A SignalR hub now upgrades to WebSocket, answers the handshake, Echo, Count streams with cancel, Fail, keep-alive Pings and the client timeout
