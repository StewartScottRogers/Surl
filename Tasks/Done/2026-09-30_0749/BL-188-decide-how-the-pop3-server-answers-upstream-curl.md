---
id: BL-188
title: Decide how the POP3 server answers upstream curl
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-184, BL-185]
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-045
created: 2026-09-29
completed: 2026-09-30
---
# BL-188 — Decide how the POP3 server answers upstream curl

## Goal

An accepted ADR decides, from measurement of the pinned upstream curl 8.21.0 build, every reply
`Surl.Protocol.Pop3` sends to what curl sends over `pop3` and `pop3s`, so BL-205, BL-206 and
BL-209 can be built without a question.

## Context

- **Measure first (ADR-0003).** `Record-CurlExchange.ps1 -Pop3` (with `-Pop3Reply`,
  `-Pop3Message`, `-Tls`) against the Windows reference build (`C:\Program Files\Git\mingw64\bin\curl.exe`,
  SHA-256 `0E7737...8778`), at least: `pop3://h/` (`LIST`); `pop3://h/1` (`RETR`); `-l`;
  `-I`; `-X UIDL`, `-X 'DELE 1'`, `-X 'TOP 1 0'`, `-X STAT`, `-X NOOP`, and what curl prints of a
  multi-line answer to a custom command; a message with a line starting with `.`; `CAPA`
  answered and refused; `--ssl-reqd` (`STLS`); `pop3s://`. Extend the script where a case needs
  it.
- **Decide:** the greeting and its `APOP` timestamp (RFC 1939 section 7's msg-id form, built
  from injected randomness so it tells nothing about the host, ADR-0006 section 3); the `CAPA`
  list per TLS state (`USER`, `SASL` per BL-185's ADR, `STLS`, `TOP`, `UIDL`,
  `PIPELINING`, `RESP-CODES`, ...); each command's reply and text; maildrop semantics (the
  message list fixed at login, `DELE` marking and `QUIT` committing to the mail store, `RSET`),
  the exclusive-access lock per BL-184's ADR; `UIDL` values from the store's UIDs; `STLS`
  without a certificate (ADR-0032 section 10); ADR-0006 section 5's POP3 column (`-ERR` then
  close; idle timeout closes with no bytes); line bounds (`--max-line`); the verbose notes
  (ADR-0033).
- **Help.** The category name and description (`pop3`, "POP3 and POP3S protocol"), for BL-209.
- Inputs: BL-184's and BL-185's ADRs, RFC 1939, RFC 2449 (`CAPA`), RFC 2595 (`STLS`),
  RFC 5034 (`AUTH`), RFC 2384 (the POP URL curl follows), ADR-0010.

## Acceptance criteria

- [x] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", records each measurement (build path, SHA-256, arguments,
      date, transcript excerpt) and decides every point in Context.
- [x] It lists the curl 8.21.0 command lines BL-212 must prove, with the expected exit code and
      stdout for each.
- [x] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help.

## Notes

- Decided in [ADR-0056](../../Documentation/Planning/Decisions/ADR-0056-how-the-pop3-server-answers-upstream-curl.md)
  from 33 recorded sessions and 6 decided-reply checks against the Windows reference build,
  2026-09-30, with `Record-CurlExchange.ps1 -Pop3`. The script needed no extension.
- Measured facts that shaped it: curl reads a `-X` reply as multi-line from the `-X` text
  alone (`-X LIST pop3://h/1` waits on a single-line `+OK`); curl prints one CRLF for an
  empty listing; `-I` changes nothing; every `-ERR` outside the login is exit 8.
- Choices with a sensible default, recorded in the ADR: `TOP` without a line count reads it
  as 0 (curl's `-X TOP pop3://h/1` sends exactly that); `UIDL` is `<uidvalidity>.<uid>`;
  `USER` accepts any name (no account enumeration); `AUTH` with no mechanism lists the
  mechanisms (RFC 1734 form); `PIPELINING`, `RESP-CODES` and `AUTH-RESP-CODE` advertised.
- `USER`/`PASS` in BL-212 is reached with `--auth basic`: it leaves the mail servers no SASL
  mechanism and no `APOP`, so curl falls to `USER`/`PASS` (measured rule, ADR-0049).

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0056 decides every POP3 reply from 33 measured curl 8.21.0 sessions and lists BL-212's command lines
