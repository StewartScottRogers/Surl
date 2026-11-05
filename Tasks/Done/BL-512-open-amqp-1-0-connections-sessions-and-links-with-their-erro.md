---
id: BL-512
title: Open AMQP 1.0 connections, sessions and links with their error conditions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-511]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-512 — Open AMQP 1.0 connections, sessions and links with their error conditions

## Goal

The AMQP 1.0 container answers `open`, `begin`, `attach`, `detach`, `end` and `close` with their state machines.
It maps channels and handles, resolves source and target addresses to nodes, keeps the idle timeout with empty
frames, and ends each misuse with the error condition the specification names.

## Context

- Decision: BL-509's ADR, decisions 3, 4 and 7.
- AMQP 1.0:
  - Part 2, section 2.4 (connections, the open/close states, and idle time-out: send an empty frame at half the
    peer's value);
  - section 2.5 (sessions, channel numbers, and `begin` with `remote-channel`);
  - section 2.6 (links, handles, attach and detach, and an attach for an unknown node answered with an attach
    with a null source or target followed by a detach carrying the error);
  - section 2.8.15 (`amqp:not-found`, `amqp:unauthorized-access`, `amqp:connection:framing-error`,
    `amqp:session:unattached-handle`, `amqp:invalid-field`).
  Part 3, section 3.5 (source and target).
- Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [x] Named tests show:
      - open, begin and attach as sender and as receiver;
      - an attach to an unknown address refused as section 2.6.3 says;
      - a frame on an unknown channel ends the connection with the right condition;
      - a transfer on an unattached handle gets `amqp:session:unattached-handle`;
      - empty frames sent at half the peer's idle time-out;
      - a silent peer closed after the container's idle time-out;
      - close from the client answered with close.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Plan, as built (ADR-0107 decisions 3, 4 and 7; no new decision was needed, so no new ADR):

- `Amqp1ContainerConnection` (partial: `.Open`, `.Sessions`, `.Links`) serves a connection after BL-511's
  `Amqp1SecurityNegotiation`, given the account it logged in. `Amqp1Session` and `Amqp1Link` hold channel and
  handle maps; `Amqp1Terminus` reads and writes source, target and coordinator; `Amqp1NodeAddress` maps addresses
  (decision 4's steps 1 to 4); `Amqp1ContainerOptions` holds the container id and the idle timeout.
  `Amqp1Descriptor` gains `Source` (0x28), `Target` (0x29) and `Coordinator` (0x30).
- Acceptance tests, all in `Amqp1ContainerConnectionTests`:
  - open, begin, attach as sender and as receiver: `ServeAsync_Open_IsAnsweredWithTheContainersOpen`,
    `ServeAsync_BeginAndAttachAsSender_IsAnsweredWithAReceivingLinkToTheQueue`,
    `ServeAsync_AttachAsReceiver_IsAnsweredWithASendingLinkFromTheNode`;
  - attach to an unknown address refused as section 2.6.3 says:
    `ServeAsync_AttachRefusedByTheDirectory_IsAnsweredWithANullTerminusThenAClosingDetach`,
    `ServeAsync_AttachAsSenderToAnAddressNamingNoNode_IsRefusedWithNotFound`;
  - frame on an unknown channel: `ServeAsync_FrameOnAChannelWithNoSession_ClosesWithFramingError`;
  - transfer on an unattached handle: `ServeAsync_TransferOnAnUnattachedHandle_EndsTheSessionWithUnattachedHandle`;
  - empty frames at half the peer's idle time-out: `ServeAsync_ClientIdleTimeOut_IsKeptWithEmptyFramesAtHalfOfIt`;
  - silent peer closed: `ServeAsync_SilentPeer_IsClosedAfterTheContainersIdleTimeout`;
  - close answered with close: `ServeAsync_Close_IsAnsweredWithClose`.
- Gates: `dotnet build` clean (warnings are errors), fast tests green, `Measure-CodeQuality.ps1` on the library:
  100% line, 100% branch, 0 failing members. BL-511's `Amqp1SecurityNegotiation.RunSaslAsync` measured cyclomatic
  complexity 12, so its outcome-writing tail moved into `AnswerLoginAsync` with no change in behaviour.

Choices with a sensible default (rule 1):

- **Node seam.** The library does not reference `Surl.MessageBus` yet: BL-513's context gives it that reference
  and the access check. BL-512 resolves nodes through a new `IAmqp1NodeDirectory` (canonical address or dynamic,
  client role, account -> found with its address, not found, unauthorized, resource limit). BL-513 backs it with
  the bus, which declares queues durable on first attach and dynamic queues exclusive and auto-deleted
  (ADR-0107 decision 4). The address rules that need no bus (no address, an empty name, a sender's wildcard,
  a coordinator) are judged here, before the directory is asked.
- **Closing.** The container writes `close` (or `open` then `close` before `open`), ends its outgoing stream and
  stops reading, as BL-511 does; it does not wait for the client's `close`.
- **A session ended with an error** discards its frames until the client's `end`, which is not answered
  (Part 2 section 2.5.5). A refused link stays in the handle map until the client's `detach`, which is not
  answered either.
- **`begin` with `remote-channel`** closes with `amqp:connection:framing-error`: the container never begins a
  session itself (decision 0), so it answers nothing.
- **No container handle free** (a client whose `handle-max` is below the handles it uses) ends the session with
  `amqp:resource-limit-exceeded`; ADR-0107 names no condition for it.
- **The idle timer runs from `open` on.** Before `open`, the client's time is the head timeout's, which spans
  headers and SASL too, so it belongs to the server that runs both - filed as BL-755 with `--max-time`'s close,
  and BL-514 now depends on it.
- `flow` without a handle, `flow` and `transfer` on attached handles and `disposition` are read and left to
  BL-513; `disposition` names delivery ids, not a handle, so the matrix row's "disposition on a handle" is BL-513's.
- **Touches.** `Documentation/Conformance/Extended/Amqp1.md` was added (rule 3; no task in Doing names it):
  ADR-0107 asks each task to replace its ID in the matrix with the named test.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The AMQP 1.0 container answers open, begin, attach, detach, end and close, maps channels and handles, resolves addresses through IAmqp1NodeDirectory, keeps both idle time-outs and ends each misuse with its error condition
