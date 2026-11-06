---
id: BL-796
title: Answer a chosen protocol error by fault in the SMTP, IMAP and POP3 servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests, Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-796 — Answer a chosen protocol error by fault in the SMTP, IMAP and POP3 servers

## Goal

When a chosen-error fault rule matches, the SMTP server answers the matching command (for example `RCPT TO` for
an address, or `DATA`) with the rule's reply code, enhanced status and text; IMAP answers the matching command
with a tagged `NO` (or `BAD`) and the rule's text; POP3 answers with `-ERR` and the rule's text; so a test can make
one send or fetch fail on purpose.

## Context

- Decision: BL-822's ADR decision 4 (what "command" and "path" mean for mail - the recipient address, the mailbox;
  reply checking; whether a fault on `DATA` reads the message first).
- Contract: BL-823 (`ExchangeContext.Faults`, `FaultAppliedEvent`). Tests use BL-823's scripted fault plan and
  recording publisher with `InMemoryConnection`.
- Where: the three servers under `Protocols/Mail`; they share `Surl.LineProtocol`, which this task does not
  change. Consult the plan per command, after any login, before the mail store.
- Request bytes from pinned upstream curl 8.21.0 (`Record-CurlExchange.ps1`), never the Curl port (ADR-0003).
  Record in Notes the exit code upstream curl 8.21.0 gives for each named case. BL-818's fault recipe uses the
  SMTP case.
- Stand-in: no store change (the message is not stored); visible as a `FaultAppliedEvent`.

## Acceptance criteria

- [x] Named tests: SMTP `RCPT TO:<bob@x>` under a rule `451 4.3.0` gets exactly that reply and nothing is
      delivered to bob, while `alice@x` is accepted; IMAP `SELECT INBOX` under a rule gets a tagged `NO` with the
      rule's text; POP3 `RETR 1` under a rule gets `-ERR` with the rule's text; each publishes one
      `FaultAppliedEvent`.
- [x] A named test per server shows a rule scoped to account `alice` fires only for `alice`.
- [x] With no rule, every existing test of the three libraries passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the three libraries keep 100% line and
      branch coverage.

## Notes

Delivered (2026-11-05, dark factory lane 5). Choices taken within ADR-0124 decision 4, recorded here because each
is a detail the ADR leaves open rather than a new decision (BL-792 did the same):

- Each session asks `ExchangeContext.Faults` once per recognized command, at `FaultStage.Command`, before the
  command touches the mail store; an unrecognized verb asks nothing. IMAP asks after its state check and POP3's
  maildrop commands after the maildrop is open, so a fault never skips a login check; SMTP asks before dispatch
  (its `MAIL` login check can only be replaced by another refusal, never bypassed).
- The point: `ExchangeId`, `ListenUrl` (the engine's reported form - `ServingEngine.FormatListenUrl` is internal
  to `Surl.Core`, so each mail library has a small `ReportedListenUrl` copy; BL-892 moves it to Abstractions),
  `Scheme`, `Account`, `Path`, `Command`.
  - Account: SMTP the `AUTH` step's `AccountName` (cleared by `STARTTLS`); IMAP the `LOGIN` user once accepted,
    or the `AUTHENTICATE` step's `AccountName`; POP3 the owner name its maildrop was opened for. Null before a
    login and for the anonymous (`--allow-anonymous`) login.
  - Path: SMTP the `RCPT TO` address as sent (`bob@x`), null for other commands; IMAP the mailbox a
    mailbox-naming command names (decoded from modified UTF-7), else the selected mailbox; POP3 none.
  - Command: the verb in capitals; IMAP `UID FETCH` and friends are asked as their own verb (`FETCH`), so one
    `command=FETCH` rule covers both forms (curl's `;UID=` URLs send `UID FETCH`).
- Only `FaultKind.Error` is applied by the servers; any other kind a plan returns at the command stage is
  ignored (command-stage delay and drop are BL-892). An error rule with no `reply` (only possible from a
  hand-built plan; the reader and `Surl.Api` require one) answers `451 4.3.0 Local error in processing`,
  `NO [UNAVAILABLE] Local error in processing`, `-ERR Local error in processing`.
- The reply is sent as the rule gives it (the reader checks its shape): SMTP the whole line, IMAP tagged
  (`<tag> NO try later`, after any pending untagged updates), POP3 `-ERR <reply>`. SMTP `DATA` answers in place
  of `354` and the transaction stays; IMAP `APPEND` answers in place of `+`; a faulted IMAP `SELECT` leaves the
  previous selection as it was.
- Each application logs `<VERB> answered with the chosen error of fault rule <id>.` and publishes one
  `FaultAppliedEvent(rule.Id, Error, Command, exchangeId, target)`, target the path when there is one, else the
  verb.

Exit codes from pinned upstream curl 8.21.0 (win-x64), recorded with `Record-CurlExchange.ps1` into each
library's `Fixtures/fault-*` (READMEs updated):

| Case | curl | Exit |
| --- | --- | --- |
| SMTP `RCPT TO:<bob@x>` answered `451 4.3.0 try later`, `--mail-rcpt-allowfails` | `fault-recipient-allowfails` | 0 (alice gets the message) |
| SMTP the same without `--mail-rcpt-allowfails` | `fault-recipient` | 55, `RCPT failed: 451` |
| SMTP `DATA` answered `451 4.3.0 try later` | `fault-data` | 55, `DATA failed: 451` |
| IMAP `SELECT INBOX` answered `NO try later` | `fault-select` | 67, `Select failed` |
| POP3 `RETR 1` answered `-ERR try later` | `fault-retr` | 8, `Weird server reply` |

Tests: `SmtpFaultTests`, `ImapFaultTests`, `Pop3FaultTests`, `ReportedListenUrlTests` in each project. Smtp 266,
Imap 752, Pop3 225 tests; the three libraries at 100% line and 100% branch (Cobertura from the MSTest
collector). Follow-up filed: BL-892 (command-stage delay and drop, and one shared `ReportedListenUrl`).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. SMTP, IMAP and POP3 answer a matching command with a chosen-error fault rule's reply (451/NO/-ERR) and publish FaultAppliedEvent
