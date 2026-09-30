---
id: BL-186
title: Decide how the SMTP server answers upstream curl
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-184, BL-185]
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-043
created: 2026-09-29
completed:
---
# BL-186 — Decide how the SMTP server answers upstream curl

## Goal

An accepted ADR decides, from measurement of the pinned upstream curl 8.21.0 build, every reply
`Surl.Protocol.Smtp` sends to what curl sends over `smtp` and `smtps`, so BL-198 to BL-200 and
BL-207 can be built without a question.

## Context

- **Measure first (ADR-0003).** `Record-CurlExchange.ps1 -Smtp` (with `-SmtpReply`, `-Tls`)
  against the Windows reference build (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E7737...8778`), at least: `--mail-from a@x --mail-rcpt b@y -T mail.txt`; two `--mail-rcpt`;
  one refused recipient with and without `--mail-rcpt-allowfails`; a `552` to `MAIL` with `SIZE`
  advertised; `-X VRFY`, `-X EXPN`, `-X HELP`, `-X NOOP` and curl with no upload (what it sends);
  an `EHLO` refused (does curl fall back to `HELO`); `--ssl-reqd` on `smtp://` (`STARTTLS`);
  `smtps://`; a message with a line starting with `.`; `--crlf`. Extend the script where a case
  needs it.
- **Decide:** the greeting and `EHLO` domain (ADR-0006 section 3: no version, nothing about the
  host); the `EHLO` capability list per TLS state (`SIZE` with `--max-filesize`, `8BITMIME`,
  `SMTPUTF8`, `PIPELINING`, `STARTTLS`, `AUTH` per BL-185's ADR); each command's reply code and
  text; recipient handling per BL-184's ADR; whether a login is needed before `MAIL`
  (ADR-0032: secure by default - decide what `--allow-anonymous` changes); `VRFY`/`EXPN`
  (RFC 5321 section 3.5.3's `252`, so accounts cannot be enumerated); whether a `Received:`
  line is added to a stored message; `STARTTLS` without a certificate (ADR-0032 section 10: refused
  in SMTP's own words, e.g. `454`); ADR-0006 section 5's SMTP column (`421`, `500`, `552` with the
  partial message discarded); the verbose notes (ADR-0033).
- **Help.** The category name and description (ADR-0034 decision 1: `smtp`, "SMTP and SMTPS
  protocol"), for BL-207.
- Inputs: BL-184's and BL-185's ADRs, RFC 5321, RFC 3207 (`STARTTLS`), RFC 1870 (`SIZE`),
  RFC 4954 (`AUTH`), ADR-0010.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", records each measurement (build path, SHA-256, arguments,
      date, transcript excerpt) and decides every point in Context.
- [ ] It lists the curl 8.21.0 command lines BL-210 must prove, with the expected exit code for
      each.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
