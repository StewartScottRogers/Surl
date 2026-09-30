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
completed:
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

- [ ] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", records each measurement (build path, SHA-256, arguments,
      date, transcript excerpt) and decides every point in Context.
- [ ] It lists the curl 8.21.0 command lines BL-212 must prove, with the expected exit code and
      stdout for each.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help.

## Notes

## Log

- 2026-09-29: Created.
