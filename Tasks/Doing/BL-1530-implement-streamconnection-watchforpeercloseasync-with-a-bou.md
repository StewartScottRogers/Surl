---
id: BL-1530
title: Implement StreamConnection.WatchForPeerCloseAsync with a bounded read-ahead buffer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1529]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1530 — Implement StreamConnection.WatchForPeerCloseAsync with a bounded read-ahead buffer

## Goal

A real connection reports a client's half-close or reset during a hold, without losing a byte the client sent first.

## Context

Amendment 2 decision 2. `Foundation/Surl.Networking.UnitLibrary/StreamConnection.cs` already keeps one peeked byte (`peekRead`, `PeekedBytePrefixStream`); generalise it to a read-ahead buffer of at most `maxBufferedBytes`, the in-flight transport read never cancelled. Read BL-1520's amendment to `Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md` (Amendment 2) first: it is the specification. Stand-in: no store; a test sees the early end by the `ExchangeEndedEvent` and by a one-connection limit freeing its slot.

## Acceptance criteria

- [ ] `StreamConnection.WatchForPeerCloseAsync` returns `PeerClosed` on read 0, `PeerReset` on `IOException`, `BufferFull` when the buffer is full, and ends without losing the in-flight read when its token is cancelled.
- [ ] The next `ReadAsync` returns the read-ahead bytes first, in order; `UpgradeToTlsAsync` hands them to the handshake ahead of the transport; `DisposeAsync` still lingers correctly.
- [ ] A bytes-then-FIN script and a bytes-then-reset script are tested on a fake stream; a client that stays loses no byte.
- [ ] Coverage 100% line and branch of the changed files; build clean with warnings as errors; `Foundation/Surl.Networking.UnitLibrary/CLAUDE.md` describes it.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
