---
id: BL-772
title: Serve SignalR Broadcast, Collect uploads, the shutdown farewell and the unsent-bytes cap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-449, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Documentation/Conformance/Extended/SignalR.md, Documentation/Planning/Decisions/ADR-0119-how-the-signalr-server-answers-signalr-and-signalrs.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-772 — Serve SignalR Broadcast, Collect uploads, the shutdown farewell and the unsent-bytes cap

## Goal

The SignalR hub serves the rest of ADR-0119 decisions 1 and 7 that BL-449 left out: `Broadcast` reaches every
connection on the hub, `Collect` reads upload streams, shutdown says goodbye, and a connection that cannot keep up
with what it is sent is closed.

## Context

- Decision: ADR-0119 decision 1 (`Broadcast`, `Collect`, a CancelInvocation of a waiting `Collect`) and decision 7
  (unsent records over 1048576 bytes, the `Collect` upload over `--max-filesize`, shutdown).
- BL-449 built the WebSocket transport in `SignalRHubSession`: the read loop, the clock, `Count` streams and the
  fixed error texts in `SignalRHubMethods`. Until this task `Broadcast` and `Collect` are answered
  `Unknown hub method.`, a client StreamItem or Completion is a protocol error, and a cancelled exchange ends with
  nothing written.
- `Broadcast` needs a hub-wide registry of the sessions that finished their handshake; `SignalRHubConnections`
  holds the hub's connections and is the natural place for it. A broadcast to a connection past the unsent-bytes
  cap is not queued for it, and that connection is closed with the protocol-error Close and WebSocket `1008`.
- Shutdown and limit cancellation: ADR-0059 and `Surl.Protocol.Ws`'s `WebSocketExchange.SayGoodbyeAtALimitAsync`
  show how a farewell is written on its own deadline.
- HubProtocol.md "Upload streaming", "Cancellation" and "Completion and results".

## Acceptance criteria

- [x] Named tests in `Surl.Extended.Protocol.SignalR.UnitTests`, through an in-memory connection, show:
      - `Broadcast` sending the non-blocking Invocation to every connection on the hub that finished its handshake,
        the caller included, then the caller's Completion with no `result`; without `invocationId`, broadcasting
        and no Completion;
      - `Collect` as an Invocation completing with the uploaded items as an array after the client's Completion of
        its stream, and as a StreamInvocation echoing each item and completing after the client's Completion;
      - a client StreamItem after its stream's Completion, or naming no upload stream, being a protocol error;
      - a `Collect` upload over `--max-filesize` completed with `The upload stream is too large.`;
      - a CancelInvocation of a waiting `Collect` completing it with no `result`;
      - a connection with more than 1048576 bytes unsent closed with the protocol-error Close and WebSocket `1008`;
      - shutdown sending `{"type":7}` then WebSocket Close `1001` within ADR-0059's grace.
- [x] `Documentation/Conformance/Extended/SignalR.md` names those tests in the rows that point at BL-768.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-11, lane 4: the finished work is still on branch `factory/BL-772-lane-1-20261111-060243`
  (commits eeecb603, ADR-0119 Amendment 1, and 651a9868, the code). Cherry-picked onto the current
  shift head, it applies cleanly apart from the task file. With `global.json`'s `rollForward` set to
  `latestMajor` in the working copy only, `Surl.Extended.Protocol.SignalR.UnitTests` builds with
  `-warnaserror` and passes 253/253. So lane 1's integration failure came from the missing pinned SDK
  (BL-1035), not the code. Once BL-1035 is Done, cherry-pick both commits, take this task file's
  version, rebuild, and finish.
- `touches` gained ADR-0119, for the amendment. BL-671 in Doing touches `Documentation/Planning/Decisions`,
  which overlaps it. That is a second reason to wait in Backlog.


- 2026-11-11 (lane 1): `FaultReplyShapes.IsWebSocketClose` now accepts only 1000-1003, 1007-1014 and 3000-4999,
  with an optional non-empty reason of at most 123 UTF-8 bytes (`Encoding.UTF8.GetByteCount`), the rule
  `WebSocketCloseCodes.IsAllowedOnTheWire` applies, written here since `Surl.Cli` may not reference `Surl.WebSocket`.
- Added both ADR files to `touches` (no task in Doing names them): ADR-0124's `ws`, `wss` row now repeats
  ADR-0134 decision 4, and ADR-0134 decision 4's "that ADR-0124's reply table lets through" became "let through
  until BL-903" so it stays true.
- Tests also cover 1005, 1015 (wss), 5000, 1003 and 1007 at the reserved range's edges, and a reason of exactly
  123 UTF-8 bytes (61 two-byte characters and one ASCII one).
- 2026-11-11, lane 5, cause found: thread-pool starvation in the test host, not surl. BL-1035's SDK worry is gone (10.0.404 rolls forward), so the win-x64 publish worked. One surl starts in ~100 ms and 32 at once all print `Listening on` within 0.5 s, so process start cost and antivirus are ruled out; both pipes are drained, so no deadlock. With 32 method-level workers blocking threads, the pool's callbacks that read surl's output ran late: the timeout message (now carrying stdout, stderr and process state) showed `Listening on ...` already in stdout, or nothing, while surl was alive. Raising the pool's minimum threads (`ThreadPoolSetup`, assembly initialize) took the FTP and DICT classes from 28 failures in 65 s to 0 in 9 s, and the full suite from 31 failures to 3-5.
- The remaining 3-5 failures per full run are all HTTP/3 `Could not bind https://127.0.0.1:<port>/: Permission denied` (exit 45): the TCP port surl draws on port 0 sits in a Windows UDP excluded range (`netsh int ipv4 show excludedportrange protocol=udp`). A surl defect outside this task's `touches`, filed as BL-1059; this task depends on it for the two clean runs. Runs after the fix: 5 and 3 failures of 1916, all HTTP/3, none a start timeout.
- 2026-11-11, lane 2: BL-1035 is Done (SDK 10.0.404 satisfies the pin). Lane 1's code did not cherry-pick onto
  the head any more: BL-755/756/757 had moved the session behind `ISignalRHubTransport` and added MessagePack.
  Redone on the new design: `Broadcast` hands the Invocation (not bytes) to each receiver so it is written in
  that connection's encoding; the unsent-bytes count and `Collect` uploads live in `SignalRHubSession`; the
  farewell (own one-second deadline, lingering close only at a limit) lives in `SignalRWebSocketTransport`.
  Collect over MessagePack returns a MessagePack array. The ADR amendment is now Amendment 2 (BL-757 took 1).
  An Ack or Sequence on a connection that did not ask for stateful reconnect was falling into the hub-Close
  case; it is a protocol error again, as the existing test says.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 1 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-772-lane-1-20261111-060243; start with git cherry-pick --no-commit factory/BL-772-lane-1-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed. The code on factory/BL-772-lane-1-20261111-060243 passes 253/253 under a temporary roll-forward. Also overlaps BL-671 on Documentation/Planning/Decisions
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Broadcast, Collect uploads, unsent-bytes cap and shutdown farewell served over the transport-based session
