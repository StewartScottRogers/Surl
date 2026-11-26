---
id: BL-1787
title: Remove the bare carriage return PASS and ACCT limit from --aihelp and the manual once BL-1785 is closed
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1785]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1787 — Remove the bare carriage return PASS and ACCT limit from --aihelp and the manual once BL-1785 is closed

## Goal

The `logging` topic of `surl --aihelp` and the manual no longer say that a tunnelled `PASS` or `ACCT` argument after a
bare carriage return can be logged.

## Context

- Filed by BL-1784, which wrote the sentence as the code was on 2026-11-26, with BL-1785 still open.
- Text: `AiHelpProse.cs` (`logging`) and `ManualText.cs` ("In tunnelled text, the argument of a `PASS` or `ACCT`
  command that follows a bare carriage return can be logged"), pinned by `AiHelpTextTests` and `ManualTextTests`.
- The sentences saying where a command or header is read may need the bare carriage return added for commands. Read
  `TunnelPasswordAndCredentialHeaderLineRule` before writing them.

## Acceptance criteria

- [x] The `logging` topic and the manual name no secret as logged that the code withholds.
- [x] `AiHelpTextTests` and `ManualTextTests` pin the changed sentences.
- [x] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

- Changed three sentences in both texts: the SOCKS paragraph now says a `PASS` or `ACCT` command and a header are each
  read at the line's start and after every bare carriage return, masked from the first secret; the `CONNECT` paragraph
  says commands after a bare carriage return are read as in a SOCKS tunnel.
- The old accepted-case sentence was replaced, not just deleted: ADR-0182 amendment 36 keeps one limit, a `PASS` or
  `ACCT` word ended by a carriage return (`PASS<CR><password>`) is not read as a command. Read in
  `TunnelPasswordAndCredentialHeaderLineRule.IsPasswordCommand`, the help now names that case.
- Verified 2026-11-26: build `-warnaserror` 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests
  30117 passed, 0 failed, 3 skipped.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. --aihelp logging and the manual now say a tunnelled PASS or ACCT after a bare carriage return is withheld
