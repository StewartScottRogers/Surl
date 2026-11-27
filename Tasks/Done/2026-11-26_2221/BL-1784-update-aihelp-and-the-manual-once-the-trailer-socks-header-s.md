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
completed: 2026-11-26
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

- [x] The `logging` topic and the manual name no secret as logged that the code withholds.
- [x] `AiHelpTextTests` and `ManualTextTests` pin the changed sentences.
- [x] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

- Read `TunnelPasswordAndCredentialHeaderLineRule`, `TunnelRedisAuthLineRule`, `TunnelImapLoginLineRule`,
  `SaslLineRedactionRule` and `HttpProxyRequestOrTunnelBytesRedactor` before editing.
- Removed three claims that are now false: a leading-blank `PASS`/`ACCT` line in both "Not withheld" paragraphs, and a
  leading-blank or bare-carriage-return header in tunnelled text. Both tunnel paragraphs now say where a command and a
  header are read.
- The trailer sentence needed no change: BL-1770 rewrote it in its own diff.
- Added the limit still open in the code: a `PASS` or `ACCT` argument after a bare carriage return can be logged
  (BL-1785). Filed BL-1787 to remove it once BL-1785 is Done.
- 2026-11-26 results: build 0 warnings, format clean, fast tests 30035 passed, 0 failed, 3 skipped. Text only, so no
  conformance or integration run.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. --aihelp logging and the manual name no tunnelled secret as logged that the redactors withhold; the open bare carriage return PASS/ACCT limit is stated (BL-1785, BL-1787)
