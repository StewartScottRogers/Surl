---
id: BL-1529
title: Add IConnection.WatchForPeerCloseAsync and use it in CommandStageFaults' connection overload
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1520]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1529 — Add IConnection.WatchForPeerCloseAsync and use it in CommandStageFaults' connection overload

## Goal

`IConnection` has the default member `WatchForPeerCloseAsync` and `CommandStageFaults`' connection overload ends a `Delay` hold when it reports the client left.

## Context

Amendment 2 decisions 2, 3 and 6. `PeerWatchOutcome` (`NotWatched`, `PeerClosed`, `PeerReset`, `BufferFull`) and the default member (returns `NotWatched` at once) live in `Foundation/Surl.Protocol.Abstractions.UnitLibrary`; `InMemoryConnection` implements it over its script; `CommandStageFaults.HoldAsync` races the delay against it. Read BL-1520's amendment to `Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md` (Amendment 2) first: it is the specification. Stand-in: no store; a test sees the early end by the `ExchangeEndedEvent` and by a one-connection limit freeing its slot.

## Acceptance criteria

- [x] `IConnection.WatchForPeerCloseAsync(int maxBufferedBytes, CancellationToken)` and `PeerWatchOutcome` exist with XML docs, the default returning `NotWatched`.
- [x] `InMemoryConnection` reports `PeerClosed` when its script ends with a half-close, `PeerReset` when its read is scripted to throw, `BufferFull` past the limit, and keeps every byte it read ahead for the next `ReadAsync`.
- [x] The connection overload of `CommandStageFaults.FindAndApplyAsync` ends a `Delay` hold at `PeerClosed` or `PeerReset` without the clock advancing, writes the note in Amendment 2 decision 3, and returns the rule; it still restarts the idle clock.
- [x] A client that stays is held the whole delay and its bytes are read in order afterwards.
- [x] 100% line and branch coverage of the changed files; build clean with warnings as errors; `Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md` describes the member.

## Notes

- InMemoryConnection gained `peerResetsWhenExhausted` (default false). Its default script half-closes, so a hold on it now ends at once; the two existing full-length hold tests pass `peerHalfClosesWhenExhausted: false`. The watch outcome is decided from the unread script length (BufferFull past the limit).
- A cancelled watch and an IOException from it are mapped inside the helper (NotWatched, PeerReset).

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. IConnection.WatchForPeerCloseAsync and PeerWatchOutcome exist; CommandStageFaults connection overload ends a Delay hold when the client closes or resets
