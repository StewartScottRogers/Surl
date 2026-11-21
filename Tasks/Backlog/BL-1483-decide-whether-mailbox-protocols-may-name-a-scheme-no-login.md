---
id: BL-1483
title: Decide whether --mailbox protocols= may name a scheme no login is made on, such as gopher or tftp
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md]
requirement: none
created: 2026-11-20
completed:
---
# BL-1483 — Decide whether --mailbox protocols= may name a scheme no login is made on, such as gopher or tftp

## Goal

An amendment to ADR-0102 decides whether `protocols=` on `--mailbox` (and `--mailbox-file`, and so `--user-ldap`'s, if it
reads the same way) may name a scheme surl serves but on which no login is ever made - `gopher`, `tftp`, `dict`, `telnet`
and the like - and the code and `--aihelp` work to make both agree is filed.

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 7:
  `--mailbox "x@example.com:pw;protocols=gopher"` starts, while `--aihelp` says an unknown scheme exits 2.
- The code and help agree on the letter: the check is "a scheme no server speaks"
  (`Application/Surl.Cli.UnitLibrary/ConfiguredMailboxReader.cs:133-134` `IsAnySurlScheme`, `CheckScheme` at `:273-277`,
  text `unknown scheme <scheme>` at `:102`; `AiHelpProse.cs:228` "a scheme no server speaks (`unknown scheme <scheme>`)"), and
  `gopher` is spoken. So the account is made, and can log in nowhere.
- The ADRs disagree on the intent: ADR-0101 decision 4 (`Documentation/Planning/Decisions/ADR-0101-surls-configuration-surface-the-config-file-repeatable-item-options-and-one-configuration-model.md:217-219`)
  says the refusal exists "so a mailbox restricted to a scheme that cannot log in is not silently unreachable"; ADR-0102
  decision 1.4 (`:181-185`) says `protocols=` "may name any scheme a registered server speaks". A login-less scheme is the
  case where the two readings part.
- Which schemes have a login is known in composition (the registrations built with `AuthenticationPolicyFor(<scheme>)`,
  `Application/Surl.Composition.UnitLibrary/ProtocolServerRegistrations.cs`), not in `Surl.Cli`, so a refusal at parse time needs
  that fact moved or a check at start instead; say which.
- Options to weigh: refuse with exit 2 (`scheme <scheme> has no login`); allow and warn at start; allow silently and correct the
  ADR-0101 sentence. Secure by default and "say what it does" (root `CLAUDE.md`) both apply. Decided by Claude under Stewart's
  delegation; no upstream curl behaviour is involved.

## Acceptance criteria

- [ ] `ADR-0102` gains an amendment, marked "Decided by Claude under Stewart's delegation", stating the rule, the exact refusal
      or warning text and exit code if any, when it is checked (parse or start), which options it covers, and why; and it
      corrects whichever of ADR-0101 decision 4's or ADR-0102 decision 1.4's sentences it overrules.
- [ ] The amendment lists the schemes that count as "no login" or says how the list is derived so it grows with new servers
      (root `CLAUDE.md`, "Open for extension").
- [ ] task-planner files the code-and-tests task and the `--aihelp`/`--manual` text task (one kind of work each), each depending
      on this task, and their IDs are in this task's Log.
- [ ] No `.cs` file changes in this task.

## Notes

## Log

- 2026-11-20: Created.
