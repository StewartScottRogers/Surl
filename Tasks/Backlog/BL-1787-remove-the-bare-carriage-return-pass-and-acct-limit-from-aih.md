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
completed:
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

- [ ] The `logging` topic and the manual name no secret as logged that the code withholds.
- [ ] `AiHelpTextTests` and `ManualTextTests` pin the changed sentences.
- [ ] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-26: Created.
