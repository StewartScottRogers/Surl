---
id: BL-1531
title: Forward WatchForPeerCloseAsync through BytePacedConnection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1529]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1531 — Forward WatchForPeerCloseAsync through BytePacedConnection

## Goal

A mail server's command-stage hold through `BytePacedConnection` still ends when the client leaves.

## Context

`BytePacedConnection` forwards `PeekByteAsync`; a wrapper that does not forward `WatchForPeerCloseAsync` hides it behind the default `NotWatched`. Read BL-1520's amendment to `Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md` (Amendment 2) first: it is the specification. Stand-in: no store; a test sees the early end by the `ExchangeEndedEvent` and by a one-connection limit freeing its slot.

## Acceptance criteria

- [x] `BytePacedConnection.WatchForPeerCloseAsync` forwards to the inner connection and returns its outcome.
- [x] A test shows an SMTP, IMAP or POP3 `Delay;stage=command` hold over `InMemoryConnection` ends at the client's half-close.
- [x] Coverage 100% of the changed files; build clean with warnings as errors.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. BytePacedConnection forwards WatchForPeerCloseAsync; a command-stage hold through it ends at the client's half-close
