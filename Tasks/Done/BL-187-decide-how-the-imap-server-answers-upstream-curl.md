---
id: BL-187
title: Decide how the IMAP server answers upstream curl
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-184, BL-185]
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-044
created: 2026-09-29
completed: 2026-09-30
---
# BL-187 — Decide how the IMAP server answers upstream curl

## Goal

An accepted ADR decides, from measurement of the pinned upstream curl 8.21.0 build, every
response `Surl.Protocol.Imap` sends to what curl sends over `imap` and `imaps`, so BL-201 to
BL-204 and BL-208 can be built without a question.

## Context

- **Measure first (ADR-0003).** `Record-CurlExchange.ps1 -Imap` (with `-ImapReply`,
  `-ImapMessage`, `-Tls`) against the Windows reference build (`C:\Program Files\Git\mingw64\bin\curl.exe`,
  SHA-256 `0E7737...8778`), at least: `imap://h/` (the mailbox list); `imap://h/INBOX`;
  `imap://h/INBOX;UID=1`; `;SECTION=TEXT`, `;SECTION=HEADER.FIELDS (SUBJECT)`, `;PARTIAL=0.100`;
  `;MAILINDEX=1`; `;UIDVALIDITY=<n>` matching and not; `imap://h/INBOX?SUBJECT%20x` (search);
  `-T mail.txt imap://h/INBOX` (APPEND, and its literal); `-X 'CREATE x'`, `-X 'EXAMINE INBOX'`,
  `-X 'STORE 1 +FLAGS \Deleted'`, `-X EXPUNGE`, and what curl prints of untagged responses to a
  custom command; `--ssl-reqd` (`STARTTLS`); `imaps://`. Extend the script where a case needs it.
- **Decide:** the protocol version (IMAP4rev1, RFC 3501, and/or IMAP4rev2, RFC 9051) and the
  `CAPABILITY` list per TLS and login state (`LOGINDISABLED`, `AUTH=` per BL-185's ADR,
  `SASL-IR`, `UIDPLUS`, ...); the greeting (no version, ADR-0006 section 3); every command curl
  sends and every RFC command it can send through `-X` (answered, or `BAD`/`NO` with the ADR's
  text - nothing left out because it is hard, root `CLAUDE.md`); `SELECT`/`EXAMINE` untagged
  data; `FETCH` item formats and literals; the `SEARCH` keys supported; `APPEND` with its
  literal bound (`--max-filesize`, tagged `NO`); flags and `\Recent`; mailbox names
  (hierarchy delimiter, `INBOX` case); `STARTTLS` without a certificate (ADR-0032 section 10);
  ADR-0006 section 5's IMAP column (`* BYE`, tagged `BAD`, tagged `NO`); literal and
  command-line bounds (`--max-line`); the verbose notes (ADR-0033).
- **Help.** The category name and description (`imap`, "IMAP and IMAPS protocol"), for BL-208.
- Inputs: BL-184's and BL-185's ADRs, RFC 3501, RFC 9051, RFC 5092 (the IMAP URL curl follows),
  RFC 4959 (`SASL-IR`), RFC 2595 (`STARTTLS`), ADR-0010.

## Acceptance criteria

- [x] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", records each measurement (build path, SHA-256, arguments,
      date, transcript excerpt) and decides every point in Context.
- [x] It lists the curl 8.21.0 command lines BL-211 must prove, with the expected exit code and
      stdout for each.
- [x] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help.

## Notes

- ADR-0055 (`ADR-0055-how-the-imap-server-answers-upstream-curl.md`) decides it, from 50 recorded
  sessions of the pinned Windows reference build on 2026-09-30 plus five sessions serving the
  decided responses (D1 to D5).
- `Record-CurlExchange.ps1` needed no extension: `-Imap`, `-ImapReply` (with `CLOSE`), `-Tls` and
  `-SaslChallenge` covered every case, so the script is unchanged and its help needs nothing.
- Findings that shaped the decisions: `imap://h/INBOX` without `;UID=` lists (`LIST "INBOX" *`);
  a body sent as a quoted string is exit 8, so every body is a literal; curl never logs in without
  `-u` (so a command before login asks the policy, as SMTP's first `MAIL` does); a `PREAUTH`
  greeting makes curl skip `-u`; with `SASL-IR` advertised curl sends the initial response unasked;
  an `APPEND` refused before `+` is exit 25 and the session can go on.
- Defaults taken (recorded in the ADR's alternatives): IMAP4rev1 only; `/` delimiter; every mailbox
  counts as subscribed; no `IDLE`, no `LITERAL+`; keywords ignored on `STORE`/`APPEND`.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0055 decides every IMAP response from 55 measured curl 8.21.0 sessions and lists BL-211's command lines
