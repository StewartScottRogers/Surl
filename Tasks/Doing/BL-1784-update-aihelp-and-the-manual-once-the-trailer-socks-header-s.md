---
id: BL-1784
title: Update --aihelp and the manual once the trailer, SOCKS header-shape and leading-blank PASS gaps are closed
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1770, BL-1772, BL-1783]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-26
completed:
---
# BL-1784 — Update --aihelp and the manual once the trailer, SOCKS header-shape and leading-blank PASS gaps are closed

## Goal

The `logging` topic of `surl --aihelp` and the manual no longer name as logged any secret that BL-1770, BL-1772 and BL-1783 made the exchange log withhold.

## Context

- Filed by BL-1764, which wrote the texts as the code was on 2026-11-26. They name as logged: a secret header in the
  trailers of a chunked request body (BL-1770); in tunnelled text, a withheld header on a line that starts with a space
  or a tab, or that follows a bare carriage return (BL-1772); and a `PASS` or `ACCT` argument on a line that starts with
  a space or a tab, in both tunnel paragraphs (BL-1783).
- Text: `AiHelpProse.cs` (`logging`) and `ManualText.cs`, pinned by `AiHelpTextTests` and `ManualTextTests`.
- Write what the code does when this task runs; read the redactors before removing a sentence.

## Acceptance criteria

- [ ] The `logging` topic and the manual name no secret as logged that the code withholds.
- [ ] `AiHelpTextTests` and `ManualTextTests` pin the changed sentences.
- [ ] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
