---
id: BL-198
title: Receive mail over SMTP into the mail store in Surl.Protocol.Smtp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-186, BL-190, BL-192]
touches: [Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests]
requirement: FR-043
created: 2026-09-29
completed: 2026-09-29
---
# BL-198 — Receive mail over SMTP into the mail store in Surl.Protocol.Smtp

## Goal

`Surl.Protocol.Smtp` has an `SmtpProtocolServer` (`IConnectionProtocolServer`, scheme `smtp`)
that greets, answers `EHLO`/`HELO`, `MAIL`, `RCPT`, `DATA`, `RSET`, `NOOP`, `VRFY`, `EXPN`,
`HELP` and `QUIT`, and delivers each accepted message into the shared mail store, as BL-186's
ADR decides (TLS is BL-199, `AUTH` is BL-200).

## Context

- Decisions: BL-186's ADR (every reply, capability list, whether a login is needed before
  `MAIL`, recipient answers); BL-184's ADR (delivery and bounds); ADR-0006 section 5's SMTP
  column (`421` for timeouts, `500` for a line past `--max-line`, `552` for a message past
  `--max-filesize` with the partial message discarded); ADR-0006 section 3.
- Until BL-200, a server that BL-186's ADR says needs a login before `MAIL` refuses `MAIL` unless
  the policy is `--allow-anonymous`; tests use `AnonymousAuthenticationPolicy`.
- Libraries: add `ProjectReference`s to `Surl.MailStore.UnitLibrary` (BL-190's store, taken in
  the constructor) and `Surl.LineProtocol.UnitLibrary` (BL-192's line reader and dot-unstuffing),
  as BL-184's ADR allows.
- Seam: ADR-0004 (`InMemoryConnection`); fixtures from BL-186's recordings under
  `Surl.Protocol.Smtp.UnitTests/Fixtures/<case>/` with a `README.md` (build, SHA-256, command
  line, date).
- Code to copy (never expectations): the Curl port's `SmtpReply.cs`, `SmtpDotStuffer.cs`,
  turned to the server's side.

## Acceptance criteria

- [x] A fast test replays each BL-186 fixture that needs neither TLS nor `AUTH` (one recipient,
      two recipients, a refused recipient, `VRFY`, `EXPN`, `HELP`, `NOOP`, a dot-stuffed body) and
      asserts surl's replies are the ADR's, byte for byte, and the stored message's bytes.
- [x] Fast tests cover: `DATA` before `RCPT`; `MAIL` with a `SIZE` over `--max-filesize`; a body
      past `--max-filesize` (`552`, nothing stored); a line past `MaxLineBytes`; the head timeout
      on a fake `TimeProvider`; the store's bound reached.
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Smtp.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no `Integration` test in `Surl.Protocol.Smtp.UnitTests` and
      no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary` reports
      100% line and branch coverage and no failing member.

## Notes

- **Fixtures.** BL-186 committed no fixture files, so the nine cases were recorded here, as
  ADR-0053's Consequences say, with `Record-CurlExchange.ps1 -Smtp` against pinned curl 8.21.0
  fed exactly the replies Surl sends (`Surl.Protocol.Smtp.UnitTests/Fixtures/README.md`). The
  refused-recipient case is recorded both without `--mail-rcpt-allowfails` (exit 55) and with it
  (exit 0). `RecordedFixtureTests` compares Surl's bytes with every `< ` line of each transcript.
- **Plan as built.** `SmtpProtocolServer(IAuthenticationPolicy, MailboxStore)` makes one
  `SmtpSession` per connection over `CrlfLineReader`; commands dispatch through a verb table so
  no method passes complexity 10. `EHLO` lists `SIZE`, `8BITMIME`, `SMTPUTF8`, `PIPELINING`,
  `ENHANCEDSTATUSCODES`; the `STARTTLS` and `AUTH` lines are BL-199's and BL-200's.
- **Defaults taken (ADR-0053 leaves them open):**
  - `STARTTLS` answers `454 4.7.0 TLS not available` (`503` on a TLS connection) and `AUTH`
    `502 5.5.1 Command not implemented` until BL-199 and BL-200; neither is advertised, so curl
    never sends them.
  - Path syntax is the store's own `LookUpRecipient` check, so SMTP and the store cannot
    disagree about what an address is. `MailRecipientPath` refuses a domainless path other than
    `postmaster`, while ADR-0053 decision 4 accepts one (row 33, `<bob>`), so a path the store
    calls invalid is tried once more with `@surl` appended; appending cannot make any other
    invalid path valid (a bad local part stays bad, and a domain gains a second `@`).
  - The `MAIL` checks run in the order sequencing, login, path, parameters; a refused `MAIL`
    starts no transaction.
  - `EHLO`/`HELO` refuse an argument holding a control byte (0x00-0x1F, 0x7F) with their `501`,
    since the domain is written into `Received` as sent; bytes above 0x7F are kept.
  - `CrlfLineReader` bounds a body by `MaxUploadBytes`, not ADR-0053's `MaxUploadBytes` less
    the trace fields, and `Surl.LineProtocol` is outside this task's `touches`. So the body is
    read into `SmtpMessageBodyBuffer`, which keeps bytes only up to that budget and reports
    passing it; either that or the reader's own `BodyTooLarge` is answered `552` and closes.
  - The store's own `MessageTooLarge` (its bound smaller than `--max-filesize`) is answered
    `552` with the session going on, since the body's end was found. The in-memory store has
    no "could not write the message" outcome yet (BL-227), so there is no `451` path; a
    `SaveChangesAsync` that throws keeps the message and is noted `Mail store: <message>`.
  - Notes are written for a line too long, a head timeout, a peer closing mid-body and a
    limit reply that misses its one-second deadline, as the DICT server does; peer bytes in a
    note go through `SmtpLogText` (GopherLogText's escaping).
- **Follow-up filed:** BL-230, the `421 4.4.2 surl Timeout, closing` for the idle timeout and
  maximum duration (ADR-0053 decision 7), which needs the engine to tell a server why its
  exchange was cancelled.
- **Measured:** `Measure-CodeQuality.ps1 -Library Surl.Protocol.Smtp.UnitLibrary`: 100% line,
  100% branch, 73 members, 0 failing, worst CRAP 10. `Surl.Protocol.Smtp.UnitTests`: 133 tests.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SmtpProtocolServer greets, answers EHLO/HELO/MAIL/RCPT/DATA/RSET/NOOP/VRFY/EXPN/HELP/QUIT and delivers into the mail store, replaying nine pinned-curl fixtures byte for byte
