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
completed: 2026-09-30
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

- [x] A fast test replays each fixture named in Context and asserts surl's responses are the
      ADR's, byte for byte.
- [x] Fast tests cover: a login accepted, refused and refused as plain-text, with the note; a
      command needing a login before it; `SELECT` of a missing mailbox; a command with a literal
      (`{n}` and the `+` continuation) and one over the bound; a line past `MaxLineBytes`; an
      unknown command (tagged `BAD`); the head timeout.
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Imap.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no `Integration` test in `Surl.Protocol.Imap.UnitTests` and
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

- Fixtures: six cases (`list-root`, `list-inbox`, `lsub`, `select`, `examine`, `status`), recorded
  by the cut-off run and re-recorded in this one to confirm them: every `request.bin`, stdout and
  exit code came out byte for byte the same. Their `README.md` gives the replies and command lines.
  The capabilities served are a policy offering the clear-password login and no SASL mechanism
  (`AUTH=` and `STARTTLS` are BL-204's), so curl logs in with `LOGIN` (ADR-0055 row 41).
- Constructor: `ImapProtocolServer(IAuthenticationPolicy, IMailAuthenticationPolicy, MailboxStore)`.
  The mail policy's `GetMailLoginOffer` decides `LOGINDISABLED` (ADR-0055 decision 2); BL-204 adds
  the `STARTTLS` flag and `AUTHENTICATE`. `SASL-IR` is advertised before a login as the ADR's list
  says, though `AUTHENTICATE` only arrives with BL-204.
- Also answered, since decision 2 advertises them and each is one line: `ID` (`* ID NIL`),
  `NAMESPACE` and `UNSELECT`. Every other command is `BAD Command not recognized` until its task.
- A line too long: the line reader returns no bytes for a first line past `--max-line`, so no tag
  was read and the answer is `* BYE surl Command line too long, closing`; a later line of the same
  command (after a literal), or the command as a whole past the bound, is `<tag> BAD Command line
  too long`. Both close (ADR-0055 decision 12).
- The head timeout bounds each line (the line reader's clock) and each literal (a clock of its own
  per literal) rather than the command as one span: `Surl.LineProtocol` is outside this task's
  `touches`, and the difference only lengthens a slow multi-literal command's allowance.
- Idle timeout and maximum duration: the engine cancels the exchange, as for SMTP; no `* BYE surl
  Timeout, closing` is written for them yet.
- A literal whose length passes `Array.MaxLength` is `BAD Literal too long` even with `--max-line
  0`, since it cannot be held in memory.
- `LIST` with an undecodable reference or pattern answers `NO [CANNOT] Invalid mailbox name`.
- A selected mailbox that another session deleted has no snapshot; it is reported as every
  message expunged.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ImapProtocolServer answers LOGIN, SELECT, EXAMINE, LIST, LSUB, STATUS, CHECK, CLOSE; six curl fixtures replay byte for byte; 100% line and branch coverage
