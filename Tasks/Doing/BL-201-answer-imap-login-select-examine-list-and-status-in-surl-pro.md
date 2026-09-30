---
id: BL-201
title: Answer IMAP LOGIN, SELECT, EXAMINE, LIST and STATUS in Surl.Protocol.Imap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-187, BL-190, BL-192]
touches: [Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests]
requirement: FR-044
created: 2026-09-29
completed:
---
# BL-201 — Answer IMAP LOGIN, SELECT, EXAMINE, LIST and STATUS in Surl.Protocol.Imap

## Goal

`Surl.Protocol.Imap` has an `ImapProtocolServer` (`IConnectionProtocolServer`, scheme `imap`)
that greets, parses tagged commands with literals, logs users in with `LOGIN` only through the
authentication contract, and answers `CAPABILITY`, `NOOP`, `LOGOUT`, `SELECT`, `EXAMINE`,
`LIST`, `LSUB`, `STATUS`, `CLOSE` and `CHECK` from the mail store, as BL-187's ADR decides.

## Context

- Decisions: BL-187's ADR (greeting, capabilities per state, every response and text, mailbox
  names); BL-184's ADR (the store); ADR-0032 criteria 1 to 3 (`LOGIN` through
  `CheckPasswordLoginAsync`; refused with no account; refused as plain-text over no TLS without
  `--allow-plaintext-auth`, with `LOGINDISABLED` as the ADR says); ADR-0038 (the login note);
  ADR-0006 section 5's IMAP column (`* BYE` for timeouts and limits; tagged `BAD` for a line too
  long once the tag was read); literal bounds as the ADR sets them.
- Libraries: add `ProjectReference`s to `Surl.MailStore.UnitLibrary` and
  `Surl.LineProtocol.UnitLibrary` (BL-184's ADR).
- Fixtures: BL-187's recordings of `imap://h/` and `imap://h/INBOX` (login, `LIST`, `SELECT`,
  `EXAMINE`) under `Surl.Protocol.Imap.UnitTests/Fixtures/<case>/` with a `README.md`.

## Acceptance criteria

- [ ] A fast test replays each fixture named in Context and asserts surl's responses are the
      ADR's, byte for byte.
- [ ] Fast tests cover: a login accepted, refused and refused as plain-text, with the note; a
      command needing a login before it; `SELECT` of a missing mailbox; a command with a literal
      (`{n}` and the `+` continuation) and one over the bound; a line past `MaxLineBytes`; an
      unknown command (tagged `BAD`); the head timeout.
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Imap.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no `Integration` test in `Surl.Protocol.Imap.UnitTests` and
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Backlog. Lane 4 could not integrate: fast tests failed twice (Surl.Protocol.Imap.UnitTests: ServeAsync_CloseWithAStoreThatCannotBeWritten_CompletesAndNotesTheFailure; then Surl.Protocol.Imap.UnitTests: ServeAsync_CloseWithAStoreThatCannotBeWritten_CompletesAndNotesTheFailure) after rebasing onto the other lanes' work. The work is on branch factory/BL-201-lane-4-20260930-012021; start with git cherry-pick --no-commit factory/BL-201-lane-4-20260930-012021 and fix it.
- 2026-09-30: Backlog -> Doing.
