---
id: BL-1633
title: Cover the Note forwarders of RedactingExchangeLog and EventPublishingExchangeLog
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1633 — Cover the Note forwarders of RedactingExchangeLog and EventPublishingExchangeLog

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Core.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Core.UnitLibrary 99.86%
  line, 100% branch, 2 failing members. Both came in with BL-1419 (commit 10ef018d8).
- `RedactingExchangeLog.Note(string)`, `Foundation/Surl.Core.UnitLibrary/RedactingExchangeLog.cs:43`, 0% line:
  `public void Note(string text) => log.Note(text);`
- `EventPublishingExchangeLog.Note(string)`, `Foundation/Surl.Core.UnitLibrary/EventPublishingExchangeLog.cs:54`, 0%
  line: the same one-line forwarder.
- Neither decorator's tests call `Note`, so nothing checks that a note passes through to the log underneath unchanged
  (not redacted, not dropped).

## Acceptance criteria

- [x] A named `Surl.Core.UnitTests` test calls `RedactingExchangeLog.Note` and asserts the inner log received exactly
      that text.
- [x] A named `Surl.Core.UnitTests` test calls `EventPublishingExchangeLog.Note` and asserts the inner log received
      exactly that text. If the class publishes nothing for a note, the test also asserts that no event was published.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Core.UnitLibrary` reports `100 | 100` with 0
      failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only: ExchangeLogNoteForwardingTests (two tests); no production change.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Note forwarders of both exchange-log decorators covered; Surl.Core.UnitLibrary 100/100, 0 failing
