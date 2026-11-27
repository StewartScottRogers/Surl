---
id: BL-1967
title: Correct the POP3 library's anonymous login, command fault and test-case reply statements
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1955]
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1967 — Correct the POP3 library's anonymous login, command fault and test-case reply statements

## Goal

The POP3 library's comments and `CLAUDE.md` say what the code does about when the anonymous login is asked, when a
command fault reaches the mail store, and what changes under an upstream test case.

## Context

Found while working BL-1955 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change.
In `Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary`:

- `Pop3ProtocolServer.cs` remarks "Logins", the comment above `WithMaildropAsync` in `Pop3Session.cs`, and `CLAUDE.md`
  "Logins": the policy is said to be asked once about the login with no credentials. `Pop3Session.UpgradeToTlsAsync`
  clears `anonymousVerdict`, so a maildrop command after `STLS` asks again.
- `Pop3Session.Faults.cs` summary and `CLAUDE.md` "Command-stage faults": an `Error` rule is said to answer before the
  mail store is touched, with the maildrop untouched. For an anonymous session's first maildrop command,
  `WithMaildropAsync` opens the maildrop before it asks the fault plan.
- `Pop3ProtocolServer.cs` remarks "Replies" and the `Pop3Replies` summary: no reply is said to echo a byte the peer
  sent. Under an upstream test case, `AnswerUpstreamTestCaseListAsync` (`Pop3Session.UpstreamTestCase.cs`) writes the
  client's `LIST` argument back.
- `Pop3Session.Faults.cs` summary and `CLAUDE.md`: every recognized command is said to ask the fault plan. Under an
  upstream test case the replacement maildrop handlers bypass `WithMaildropAsync`, so those commands do not ask it.

No store, event or fault is changed: documentation only.

## Acceptance criteria

- [ ] The "Logins" text in all three places says a refused or not-yet-asked anonymous login is asked again after `STLS`.
- [ ] The command-fault text says an anonymous session's first maildrop command opens the maildrop before an `Error` rule answers it.
- [ ] The no-echo statements except the `LIST` reply under an upstream test case.
- [ ] The command-fault text says the maildrop commands answered from an upstream test case's data ask no fault plan.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
