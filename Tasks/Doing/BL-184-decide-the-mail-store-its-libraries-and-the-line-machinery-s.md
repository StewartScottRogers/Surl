---
id: BL-184
title: Decide the mail store, its libraries and the line machinery SMTP, IMAP and POP3 share
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md]
requirement: FR-047
created: 2026-09-29
completed:
---
# BL-184 — Decide the mail store, its libraries and the line machinery SMTP, IMAP and POP3 share

## Goal

An accepted ADR decides the mail store the SMTP, IMAP and POP3 servers share (its model, its
bounds, its persistence under `<path>/.surl/mail`) and the line-oriented machinery they share,
and adds the two horizontal libraries that hold them to ADR-0002's reference table, so BL-189
to BL-192 can be built without a question.

## Context

- Three protocol servers may not reference each other (ADR-0002 decision 3, root `CLAUDE.md`),
  so what they share lives in horizontal libraries that join ADR-0002's table through a new ADR.
  The plan (BL-189 to BL-192, BL-198 to BL-209) already uses two, and the ADR adopts them and
  records why:
  - `Surl.MailStore.UnitLibrary`: the mail store - mailboxes per account, messages with UIDs
    and `UIDVALIDITY`, flags, internal dates from the injected `TimeProvider`, delivery,
    append, expunge, mailbox create/delete/rename, bounds, safe for concurrent sessions, and its
    persistence through `IContentFileSystem`. May reference `Surl.Protocol.Abstractions` and
    `Surl.Content` (for `IContentFileSystem`).
  - `Surl.LineProtocol.UnitLibrary`: CRLF command lines read from an `IConnection` bounded by
    `ExchangeLimits.MaxLineBytes` and the head timeout without ever reading past the limit
    (ADR-0006), reply writing, dot-stuffing and unstuffing (RFC 5321 section 4.5.2, RFC 1939
    section 3) bounded by `--max-filesize`, ADR-0010's rule that bytes read past a `STARTTLS`/
    `STLS` line are discarded before the upgrade, and base64 SASL continuation lines with the
    `*` cancel. May reference `Surl.Protocol.Abstractions` only.
  Two libraries rather than one let two lanes build them at once, and a store is not line
  machinery. Whether FTP (already planned on its own reader, BL-177) later adopts
  `Surl.LineProtocol` is a follow-up the ADR may note, not a dependency.
- **The model to decide.** Whose mailboxes exist (the accounts of `--user`/`--user-file`, ADR-0032;
  what an empty-name Bearer account has); how an SMTP `RCPT TO` address maps to a mailbox (local
  part, domain ignored or not), and what an unknown recipient gets without telling a peer whether
  an account exists (ADR-0006 section 3, ADR-0032 section 8 - weigh RFC 5321's `550` against that
  rule and record the choice); where mail goes with `--allow-anonymous` and no accounts; IMAP's
  `INBOX` and other mailboxes; POP3's maildrop as `INBOX`, and RFC 1939's exclusive-access lock;
  whether `--allow-uploads` gates IMAP `APPEND` and SMTP delivery (ADR-0006 section 2 names
  uploads into the served files; mail is service state, ADR-0031).
- **Bounds** (ADR-0006: every store a peer can fill is bounded): message size by
  `--max-filesize`; a total bound on messages and bytes (constants, as ADR-0014 decision 7 and
  ADR-0031 decision 4 set theirs); what a delivery past the bound gets in each protocol's words.
- **Persistence** (ADR-0031 decision 6's pattern, "a later service keeps its state under
  `<path>/.surl/<service>/`"): the byte format under `<path>/.surl/mail`, whole-file or
  per-message writes through a temporary name and `MoveFileReplacing`, loaded once at start
  after the data-directory lock, a malformed store refused with `CouldNotReadFile` (37) and its
  text, a write failure noted and not fatal; in memory without `--directory`.
- Inputs: ADR-0002, ADR-0006, ADR-0010, ADR-0014 decision 7, ADR-0031, ADR-0032, and
  `Documentation/Product/Product-Overview.md` "Layers" and "Project layout".

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", decides every point in Context, with the two libraries'
      contents and reference rows, the model, the bounds and their constants, and the persisted
      byte format precise enough to pin in a test.
- [ ] `ADR-0002-mirror-the-curl-ports-project-map.md` carries an "Amended" line naming the new
      ADR.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the ADR.
- [ ] `Documentation/Product/Product-Overview.md` "Layers" and "Project layout" list the two
      libraries, written as intent until BL-189 creates them.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
