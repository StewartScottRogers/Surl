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
completed:
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

- [ ] A fast test replays each fixture named in Context and asserts surl's replies byte for byte.
- [ ] Fast tests cover: a login accepted, refused and refused as plain-text, with the note; a
      command before login; `RETR` of a deleted and of a missing message; a message line starting
      with `.` stuffed on the wire; `DELE` then `RSET`; `DELE` then `QUIT` removing the message from
      the store and a dropped connection not removing it; the maildrop lock refusing a second
      session as the ADR says; a line past `MaxLineBytes`.
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Pop3.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no `Integration` test in `Surl.Protocol.Pop3.UnitTests` and
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Pop3.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
