---
id: BL-268
title: Decide one name for the TLS-upgrade constructor parameter across FTP, SMTP, IMAP and POP3
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-264]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-30
completed:
---
# BL-268 — Decide one name for the TLS-upgrade constructor parameter across FTP, SMTP, IMAP and POP3

## Goal

An ADR, marked "Decided by Claude under Stewart's delegation", decides whether the one setting
"whether this server offers a TLS upgrade" keeps a per-command name in each server's constructor
or takes one name everywhere, and files the rename task(s) the decision needs.

## Context

- Found by BL-214 (documenting Phase 3 mail, 2026-09-30). `Documentation/Wiki/Glossary.md`'s
  "TLS upgrade" row lists the names as they are: `isTlsUpgradeAvailable` in
  `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` (from
  `ServerTlsComposition.IsCertificateConfigured`), passed as `isStartTlsAvailable` to
  `SmtpProtocolServer` and `ImapProtocolServer` (and on into `SmtpSession`, `ImapSession`),
  `isStlsAvailable` to `Pop3ProtocolServer` (and `Pop3Session`), and `isAuthTlsAvailable` to
  `FtpProtocolServer` (and `FtpCommandResponder`). Their tests use the same names
  (`*TestExchange.cs`, `SmtpAuthTests`, `ImapStartTlsTests`, `Pop3StlsTests`, `FtpTlsTests`,
  `RecordedFixtureTests`, `RecordedTlsTests`).
- BL-264 (Backlog, depended on here) already renames FTP's `isAuthTlsAvailable` to
  `isTlsUpgradeAvailable`, calling that the Glossary term. This task runs after it, so the ADR
  starts from FTP named `isTlsUpgradeAvailable` and the three mail servers named after their
  command; it must say whether that mixed state is the decision or a step towards one name.
- Rules to decide by: root `CLAUDE.md` "Say what it does, do what it says" - one concept has one
  name, the one in the glossary; root `CLAUDE.md` "Decisions" - decided by Claude, recorded in an
  ADR, no question to Stewart. The case for per-command names is that each reads as the command
  it gates (`STARTTLS`, `STLS`, `AUTH TLS`); the case for one name is that it is one setting
  with one source and one glossary term.
- No upstream curl measurement is needed: this is a naming decision with no change on the wire.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/`, with the next free ADR number, marked
      "Decided by Claude under Stewart's delegation", states the chosen name (or names) for the
      constructor parameter, the properties or fields behind it and the test helpers' parameters in
      `Surl.Console`, `Surl.Protocol.Ftp`, `Surl.Protocol.Smtp`, `Surl.Protocol.Imap` and
      `Surl.Protocol.Pop3`, and why, naming the alternative it rejected.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.
- [ ] Every rename the ADR decides is filed with `task-board.ps1 new` as a `docs` task assigned to
      Claude (renames only, no behaviour change), whose `touches` name exactly the libraries, their
      `.UnitTests` twins and `Documentation/Wiki/Glossary.md` it changes, and which updates the
      glossary's "TLS upgrade" row; the ADR names each task's ID. If the ADR decides the names stay
      as they are after BL-264, it says so and no task is filed.
- [ ] No `.cs` file changes in this task.

## Notes

- `touches` is the Decisions folder only; the renames themselves, and the glossary row, belong to
  the tasks this ADR files, so they can run beside other work.

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-214; waits on BL-264, which renames the FTP parameter.
