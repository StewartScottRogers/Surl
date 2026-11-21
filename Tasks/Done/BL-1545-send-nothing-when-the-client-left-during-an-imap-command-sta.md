---
id: BL-1545
title: Send nothing when the client left during an IMAP command-stage delay hold
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1540]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1545 — Send nothing when the client left during an IMAP command-stage delay hold

## Goal

An IMAP command whose `delay` hold ended because the client left is not answered and the session ends; a staying client gets the same bytes as before.

## Context

ADR-0141 Amendment 3 (ImapSession.Faults.cs), using the BL-1540 helper result.

## Acceptance criteria

- [x] The goal holds, shown by named unit tests that fail without the change.
- [x] dotnet build is clean, fast tests pass, 100% line and branch coverage of the changed library.

## Notes

A client that leaves is told by `ClientLeft` of `FindAndApplyWithClientOutcomeAsync` (PeerClosed or PeerReset): the session returns false, no answer. The existing delay test now serves a client that stays (`peerHalfClosesWhenExhausted: false`), as an exhausted in-memory peer otherwise counts as having left.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. IMAP sends nothing and ends the session when the client leaves during a command-stage delay hold
