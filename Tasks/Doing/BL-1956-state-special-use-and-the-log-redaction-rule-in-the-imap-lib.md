---
id: BL-1956
title: State SPECIAL-USE and the log redaction rule in the IMAP library's CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1956 — State SPECIAL-USE and the log redaction rule in the IMAP library's CLAUDE.md

## Goal

The IMAP library's `CLAUDE.md` says when `SPECIAL-USE` is offered and says that the server redacts credentials from
the exchange log.

## Context

Found while working BL-1947 (2026-11-27), read from source. `CLAUDE.md` text only; no behaviour change.

In `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary/CLAUDE.md`, under "What it holds":

- About lines 38-39 say "`IDLE` and the other extensions of decision 2's list are not offered". ADR-0055 decision 2's
  list includes `SPECIAL-USE`, but `ImapCapabilities.List` adds `SPECIAL-USE CREATE-SPECIAL-USE` when the store holds a
  folder use (ADR-0055, Amendment 1).
- About line 23 lists `ImapProtocolServer` as `IConnectionProtocolServer` and `IConnectionRefusalWriter`. The class also
  implements `IExchangeBytesRedactingServer`, and the file never mentions `ImapLineRedactionRule` (ADR-0183,
  decision 5), which withholds `LOGIN` passwords and `AUTHENTICATE` secrets from the exchange log.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] `CLAUDE.md` says `SPECIAL-USE CREATE-SPECIAL-USE` are offered when the store holds a folder use, and no sentence says they are not offered.
- [ ] `CLAUDE.md` names `IExchangeBytesRedactingServer` and `ImapLineRedactionRule`, and says what the rule withholds.
- [ ] No `.cs` file changes.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
