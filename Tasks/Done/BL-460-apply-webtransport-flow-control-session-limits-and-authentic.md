---
id: BL-460
title: Apply WebTransport flow control, session limits and authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-459]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-460 — Apply WebTransport flow control, session limits and authentication

## Goal

The WebTransport server enforces the flow-control limits and the session limit BL-457's ADR set, sending and
honouring the flow-control capsules. It requires a login on the CONNECT unless `--allow-anonymous` is given.

## Context

- Decision: BL-457's ADR, decisions 4, 5, 7 and 8.
- WTH3 "Flow Control" (`WT_MAX_DATA`, `WT_MAX_STREAMS`, `WT_DATA_BLOCKED`, `WT_STREAMS_BLOCKED`) and
  the session limit of WTH3 5.1 and 5.2 (draft -16 has no `SETTINGS_WT_MAX_SESSIONS`; ADR-0117 decision 5).
- The login goes through the `Surl.Protocol.Abstractions` contracts.

## Acceptance criteria

- [x] Named tests show:
      - a session over ADR-0117 decision 5's limit (1 without flow control, 16 with) reset `H3_REQUEST_REJECTED`;
      - a client exceeding `WT_MAX_STREAMS` ending the session with the draft's error;
      - data past `WT_MAX_DATA` refused;
      - `WT_MAX_DATA` raised as the server reads;
      - a CONNECT without a login refused;
      - one with a good login accepted;
      - an anonymous CONNECT accepted only under `--allow-anonymous`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Tests, by criterion: `WebTransportConnectHandlerTests.ServeSessionAsync_SecondSessionWithoutFlowControl_IsResetRequestRejected`
  and `..._SeventeenthSessionWithFlowControl_IsResetRequestRejected`;
  `WebTransportSessionTests.ServeStreamAsync_ClientStreamPastWtMaxStreams_EndsTheSessionWithFlowControlError`,
  `..._PayloadPastWtMaxData_EndsTheSessionWithFlowControlError`,
  `..._HalfTheDataWindowRead_RaisesWtMaxDataToTheBytesReadPlusTheWindow`;
  `WebTransportConnectAuthenticationTests.RefuseAsync_ConnectWithoutALogin_IsAnswered401WithTheRequestBoundChallengesAndFin`,
  `..._ConnectWithTheAccountsLogin_IsAccepted`, `..._AnonymousConnectUnderAllowAnonymous_IsAccepted`.
- Coverage measured with the MSTest coverage collector (cobertura): 100% line, 100% branch on the library.
  203 tests in the project (was 145); the suite ran 15 times in a row green after fixing races in tests that
  checked `STOP_SENDING` before it arrived (one was the pre-existing `ClientSettingsLackARequirement` test).
- Choices taken (all within ADR-0117; no new ADR needed):
  - `WebTransportApplicationProtocol` now takes the `IAuthenticationPolicy` (as `Http3ApplicationProtocol`
    does); nothing composes it yet (BL-461), so the public constructor change breaks no caller.
    `--allow-anonymous` and the refusal delay are the policy's, so the server only calls `JudgeAsync`.
  - The judging order is origin (403), login (401/403), path (405), session limit (reset) - decision 1's table.
  - NTLM/Negotiate credentials are stripped and the request re-judged without them; the answer is `401`
    with that verdict's challenges minus NTLM and Negotiate, whatever its outcome, so they are never served
    as anonymous. Challenges naming either scheme are dropped from every `401`.
  - Raise rules read from decision 4: a stream limit becomes closed + 100 once no more than half the
    credit is left (checked on each open and close, so a client never waits on a raise that needs a
    further close); the data limit becomes read + window once half the window is read, capped at
    `--max-filesize`, and sent only when it grows.
  - Flow-control initial limits are an internal `WebTransportFlowControlSettings` record (default 100 /
    1048576) so tests use 2 streams and an 8-byte window; with QUIC's own 100-stream limit a WT stream
    violation is otherwise unreachable from a well-formed client.
  - With flow control off, `WT_MAX_STREAM_DATA` and `WT_STREAM_DATA_BLOCKED` are ignored like every other
    flow-control capsule (WTH3 5.1 "MUST ignore"); with it on they reset `H3_MESSAGE_ERROR`.
  - `--max-filesize` counts stream payload and QUIC HTTP datagrams; `DATAGRAM` capsules are not counted
    (they are bounded by `--max-message` per capsule already). "Reaches" is `>=`.
  - `WebTransportConnectJudgement.Judge` was removed: the login sits between its two checks, so the handler
    calls `IsOwnOrigin` and `IsAcceptedPath` directly.
- Follow-ups filed: BL-896 (the 64-datagram echo cap of decision 8, conformance row Surl-8-3) and BL-897
  (point the conformance matrix's BL-460 rows at these tests; `Documentation/Conformance` was held by
  BL-879, so this lane left it alone).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. WebTransport enforces flow control both ways, the 1/16 session limit, the --max-filesize close and CONNECT logins
