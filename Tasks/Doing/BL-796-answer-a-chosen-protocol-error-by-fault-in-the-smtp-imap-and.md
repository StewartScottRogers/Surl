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
completed:
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

- [ ] Named tests: SMTP `RCPT TO:<bob@x>` under a rule `451 4.3.0` gets exactly that reply and nothing is
      delivered to bob, while `alice@x` is accepted; IMAP `SELECT INBOX` under a rule gets a tagged `NO` with the
      rule's text; POP3 `RETR 1` under a rule gets `-ERR` with the rule's text; each publishes one
      `FaultAppliedEvent`.
- [ ] A named test per server shows a rule scoped to account `alice` fires only for `alice`.
- [ ] With no rule, every existing test of the three libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the three libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
