---
id: BL-772
title: Serve SignalR Broadcast, Collect uploads, the shutdown farewell and the unsent-bytes cap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-449]
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

- Delivered (2026-11-11, dark factory lane 1): `SignalRHubMethods` serves `Broadcast` and `Collect`;
  `SignalRHubConnections` holds the sessions past their handshake (`Join`, `Leave`, `Broadcast`, through
  the new internal `ISignalRBroadcastReceiver`); `SignalRHubSession` reads uploads (`SignalRUpload`),
  cancels a waiting or streaming `Collect`, counts unsent bytes and closes a connection a broadcast would
  take past 1048576 with the protocol-error Close and `1008`, and says the farewell (`{"type":7}`, `1001`)
  when the exchange is cancelled.
- Decisions recorded as ADR-0119 Amendment 1 (Decided by Claude under Stewart's delegation): the
  farewell rides the exchange's cancellation on a one-second deadline of its own, because `Surl.Core`
  signals shutdown only after the grace (an exception to ADR-0059 decision 4, for this server only);
  the unsent cap counts every queued record; the upload limit is "past", `0` meaning none; a `Collect`
  completed early drops the rest of its upload until the client's Completion; a client Completion
  with `error` ends the upload like one without; a stream ID already being uploaded is a protocol error.
- `touches` gained ADR-0119 for that amendment: no task in `Doing` names it.
- The acceptance criteria point at "the rows that point at BL-768"; the matrix's rows actually named
  BL-772, and those are the rows now naming tests (plus HUB-Cancel-1 for the `Collect` cancellation).
- Measured: 253 tests in `Surl.Extended.Protocol.SignalR.UnitTests`, 100% line and 100% branch
  coverage of the library (cobertura from the SDK's collector); the whole solution builds with
  `-warnaserror` and all 79 fast test assemblies pass.
- Environment: the .NET SDK 10.0.401 that `global.json` pins is no longer installed on this machine
  (only 10.0.100-rc.1 and 11.0.100), so `dotnet` refuses to start. The build and tests ran with
  `rollForward` set to `latestMajor` in the working copy only (SDK 11.0.100, targeting `net10.0`,
  running on the installed 10.0.15 runtime); `global.json` was restored and is not committed.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. SignalR hub serves Broadcast, Collect uploads (with --max-filesize and cancellation), the 1 MiB unsent-bytes cap and the 1001 farewell at shutdown or a limit
