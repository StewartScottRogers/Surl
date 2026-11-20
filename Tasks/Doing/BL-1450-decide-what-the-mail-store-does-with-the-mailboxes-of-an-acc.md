---
id: BL-1450
title: Decide what the mail store does with the mailboxes of an account added or removed while the server runs
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-20
completed:
---
# BL-1450 — Decide what the mail store does with the mailboxes of an account added or removed while the server runs

## Goal

An ADR, "Decided by Claude under Stewart's delegation", fixes what the one mail store does when an
account is added or removed while the server runs (surl-mcp `add_account`/`remove_account`, `Surl.Api`
`SurlServer.Accounts.Add`/`Remove`, control endpoint `POST`/`DELETE /v1/accounts`). An added account
gets the mailbox a start-time `--user` gets; a removed account's mail stops being delivered and served.

## Context

- Surly bug report `2026-11-19T2203-Surl-bug-report.md` items 1 and 2 (Surl `763754785`, unchanged
  since). (1) `add_account task-1` with password `pw`, then
  `curl -u task-1:pw "imap://127.0.0.1:PORT/INBOX?ALL"`: the login is accepted, then `NO [NONEXISTENT]`
  and curl exit 67; `seed_mail` to `task-1` is refused `NoSuchAccount`. (2) `remove_account` on a
  start-time account keeps its mailbox, and mail is still delivered to it.
- Cause: `Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs:132-150`. The constructor fixes the
  owners from the start's account names (`ReachOwners`) and records `configuredMailboxes` for `Reset`.
  `Application/Surl.Composition.UnitLibrary/ComposedStores.cs:623` (`AddAccount`) and `:630`
  (`RemoveAccount`) change only the `AccountBook`. Every host reaches these two through
  `ComposedStores`: `ComposedControlTarget.cs:176-185`, `Application/Surl.Api/SurlServerAccounts.cs:42,63`,
  and the surl-mcp tools through `Surl.Api`.
- Decided already: ADR-0124 decision 3 (accounts as a controllable store), ADR-0131 (mail store seed,
  reset, snapshot, restore: `Reset` recreates exactly the mailboxes each owner had at construction),
  ADR-0161 (`remove_account`), ADR-0102 decision 1 (mail domains: an account is a recipient by local
  part). None says what an added or removed account does to the mail store. `MailboxStore.LoadAsync`
  already keeps owners "that are no longer accounts ... unreached", the precedent for a removal.
- Questions the ADR answers: does an added account get `INBOX` only, or also `--mailbox`-style folders;
  is a removed owner's mail kept unreached or deleted, and does re-adding the name bring it back; what
  `Reset`, snapshot and restore do with owners added or retired at run time (the account store resets
  to the configured accounts); which `StoreChangedEvent`s are published; what happens under
  `--allow-anonymous` (one anonymous owner), with `--mail-domain` and with `--directory` (persisted
  index).

## Acceptance criteria

- [ ] An ADR in `Documentation/Planning/Decisions` (the next free number), marked "Decided by Claude
      under Stewart's delegation", answers each question above with the outcome a test can observe
      (IMAP `SELECT INBOX`, POP3 `STAT`, SMTP `RCPT TO` reply, `seed_mail` result, events published).
- [ ] It says how ADR-0131's reset and restore treat run-time owners, and amends ADR-0131 if that
      changes its text.
- [ ] It names the work split as BL-1451 (`Surl.MailStore`) and BL-1452 (`Surl.Composition`), with each
      new member's name and its result type.
- [ ] `Documentation/Planning/Decisions/README.md` lists the ADR.

## Notes

- Leaning: an added account gets an empty `INBOX` (as `ReachOwners` gives a start-time account), and a
  removed one is retired: unreached, not deleted, so re-adding the same name in the same run gets its
  mail back, and a reset drops run-time owners as the account store drops run-time accounts.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T2203-Surl-bug-report.md` items 1 and 2.
- 2026-11-20: Backlog -> Doing.
