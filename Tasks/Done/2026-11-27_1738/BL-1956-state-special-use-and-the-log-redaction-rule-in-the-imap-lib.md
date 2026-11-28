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
completed: 2026-11-27
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

- [x] `CLAUDE.md` says `SPECIAL-USE CREATE-SPECIAL-USE` are offered when the store holds a folder use, and no sentence says they are not offered.
- [x] `CLAUDE.md` names `IExchangeBytesRedactingServer` and `ImapLineRedactionRule`, and says what the rule withholds.
- [x] No `.cs` file changes.

## Notes

- `align-and-document` made the edit under "What it holds", checking each statement against the source. The redaction
  text also cites ADR-0183 amendment 1, which settles the literal question decision 5 left open.
- Where ADR-0055 Amendment 1 point 3 ("decided at start") and the code (asked each time the capability list is written)
  differ, `CLAUDE.md` follows the code. BL-1975 corrects the ADR or the code.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: no failures.
- Follow-ups filed: BL-1974 (`CREATE` with an empty `USE` list), BL-1975 (ADR and XML comment wording), BL-1976
  (folder uses not persisted).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The IMAP library's CLAUDE.md states when SPECIAL-USE is offered and what the exchange log redacts
