---
id: BL-205
title: Answer POP3 USER, PASS and the maildrop commands in Surl.Protocol.Pop3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-188, BL-190, BL-192]
touches: [Surl.Protocol.Pop3.UnitLibrary, Surl.Protocol.Pop3.UnitTests]
requirement: FR-045
created: 2026-09-29
completed: 2026-09-30
---
# BL-205 — Answer POP3 USER, PASS and the maildrop commands in Surl.Protocol.Pop3

## Goal

`Surl.Protocol.Pop3` has a `Pop3ProtocolServer` (`IConnectionProtocolServer`, scheme `pop3`) that
greets, logs users in with `USER`/`PASS` only through the authentication contract, and answers
`CAPA`, `STAT`, `LIST`, `UIDL`, `RETR`, `TOP`, `DELE`, `RSET`, `NOOP` and `QUIT` against the
user's maildrop in the mail store, as BL-188's ADR decides (`STLS`, `AUTH` and `APOP` are
BL-206).

## Context

- Decisions: BL-188's ADR (greeting and its timestamp, `CAPA`, every reply and text, maildrop
  semantics); BL-184's ADR (the store, the exclusive-access lock); ADR-0032 criteria 1 to 3
  (`USER`/`PASS` through `CheckPasswordLoginAsync`; refused with no account; `PASS` over no TLS
  refused unchecked without `--allow-plaintext-auth`); ADR-0038 (the login note); ADR-0006
  section 5's POP3 column (`-ERR` then close for limits; the idle timeout closes with no bytes).
- `RETR` and `TOP` write dot-stuffed multi-line answers with BL-192's writer; `DELE` marks, and
  only `QUIT` in the TRANSACTION state commits deletions to the store (RFC 1939 section 6).
- Libraries: add `ProjectReference`s to `Surl.MailStore.UnitLibrary` and
  `Surl.LineProtocol.UnitLibrary` (BL-184's ADR). Randomness for the greeting timestamp is
  injected.
- Fixtures: BL-188's recordings of `pop3://h/`, `pop3://h/1`, `-l`, `-I` and the `-X` commands,
  under `Surl.Protocol.Pop3.UnitTests/Fixtures/<case>/` with a `README.md`.

## Acceptance criteria

- [x] A fast test replays each fixture named in Context and asserts surl's replies byte for byte.
- [x] Fast tests cover: a login accepted, refused and refused as plain-text, with the note; a
      command before login; `RETR` of a deleted and of a missing message; a message line starting
      with `.` stuffed on the wire; `DELE` then `RSET`; `DELE` then `QUIT` removing the message from
      the store and a dropped connection not removing it; the maildrop lock refusing a second
      session as the ADR says; a line past `MaxLineBytes`.
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Pop3.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no `Integration` test in `Surl.Protocol.Pop3.UnitTests` and
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Pop3.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

- Built as ADR-0056's Consequences split it: decisions 1, 2 without the timestamp, 3 without
  `SASL` and `STLS`, 4 to 7 with `USER`/`PASS` as the login, 9 and 10. `Pop3ProtocolServer`
  (scheme `pop3`, also `IConnectionRefusalWriter`), `Pop3Session`, `Pop3Replies`,
  `Pop3CommandLine`, `Pop3Maildrop`, `Pop3TopSection`. The constructor takes both policies and the
  store; no randomness is injected yet, since the greeting timestamp is BL-206's.
- Until BL-206: `STLS` answers decision 8's no-certificate `-ERR STLS not available`; `APOP`
  answers `-ERR Unsupported authentication mechanism` (the greeting carries no timestamp, as
  decision 4 says); `AUTH <mechanism>` the same (nothing is offered); bare `AUTH` the RFC 1734
  listing, empty. After a login each is `-ERR Already logged in`.
- Fixtures were recorded afresh (BL-188 left none in the repository): 21 cases under
  `Surl.Protocol.Pop3.UnitTests/Fixtures/` with a `README.md`, each fed ADR-0056's exact replies
  through `-Pop3Reply` so curl accepted Surl's bytes before any test pinned them. The recorder
  needed no extension.
- Default taken: decision 1's "a message number from 1 to the view's count, else Invalid arguments"
  and decision 5's "past the view is No such message" overlap; a well-formed number (1 to 10
  digits, value at least 1) past the view or deleted is `-ERR No such message` (row 15 measured
  that), anything else `-ERR Invalid arguments`.
- Default taken: `QUIT`'s UPDATE removes the marked messages with `MaildropLock.RemoveMessages`,
  which never refuses; the store's only failure is `SaveChangesAsync` throwing, which keeps the
  change in memory for the next save (ADR-0050 decision 7). So that is noted
  (`Mail store: <message>`) and `QUIT` is still `+OK surl signing off`, as SMTP answers `250`
  when its save throws. `-ERR [SYS/TEMP] Some deleted messages not removed` stays unused until
  the store can refuse a removal.
- Default taken: a message that cannot be read at `RETR`/`TOP` (its file gone) throws to the
  engine, which ends the exchange; curl then sees a close (56). No reply was decided for it.
- Default taken: `USER`'s name is one argument word of 0x21 to 0x7E, per decision 1's argument
  split; anything else is `-ERR Invalid arguments`, so the login note never carries a control byte.
- `Measure-CodeQuality.ps1 -Library Surl.Protocol.Pop3.UnitLibrary`: 100% line, 100% branch, 83
  members, 0 failing, worst CRAP 8. 121 tests in `Surl.Protocol.Pop3.UnitTests`, none
  `Integration`, none opening a socket.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pop3ProtocolServer answers USER/PASS, CAPA, STAT, LIST, UIDL, RETR, TOP, DELE, RSET, NOOP and QUIT against the mail store's maildrop, replaying 21 upstream curl fixtures byte for byte
