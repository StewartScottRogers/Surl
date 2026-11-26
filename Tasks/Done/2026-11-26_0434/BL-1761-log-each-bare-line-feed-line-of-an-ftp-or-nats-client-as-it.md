---
id: BL-1761
title: Log each bare-line-feed line of an FTP or NATS client as it arrives
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-25
completed: 2026-11-26
---
# BL-1761 — Log each bare-line-feed line of an FTP or NATS client as it arrives

## Goal

An FTP or NATS client that ends lines with a bare line feed has each line in the exchange log when that line arrives.

## Context

- Found while working BL-1757. `LineBytesRedactor` ends a line only at CR LF, while the FTP and NATS readers also end one
  at a bare LF. So such a client's lines are held until a CR LF arrives or the connection closes, and past 64 KiB they
  are logged as `****` and dropped. No secret is logged. Read from the code; no test reproduces it yet.
- ADR-0182 decision 6 sets the line redactor's behaviour; amend it if the shared redactor changes.

## Acceptance criteria

- [x] A named test for FTP and one for NATS shows a line ended by a bare LF written to the log before any CR LF or flush,
      with its secret masked.
- [x] A named test shows a protocol that ends lines only at CR LF logging as it does today.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the changed libraries stay at 100% line and branch
      coverage.

## Notes

- `LineBytesRedactor` takes an optional `endsLineAtBareLineFeed` (default `false`); FTP and NATS pass `true`, and
  their rules no longer split at LF. Recorded as ADR-0182 amendment 31, with the alternatives rejected there. A
  constructor argument was chosen over a rule member because the line end belongs to the redactor.
- Tests: `FtpExchangeLogRedactionTests.Pass_EndedByABareLineFeed_IsLoggedMaskedAsItArrivesBeforeAnyCrLfOrFlush`,
  `NatsExchangeLogRedactionTests.Connect_EndedByABareLineFeed_IsLoggedMaskedAsItArrivesBeforeAnyCrLfOrFlush`,
  `LineBytesRedactorTests.Redact_ARedactorEndingLinesOnlyAtCrLf_HoldsABareLineFeedLineUntilCrLf`.
- No `--aihelp` or manual change: no option, and no secret newly logged or withheld.
- Verified 2026-11-26: build `-warnaserror` clean, `dotnet format --verify-no-changes` clean, fast tests green, and
  `Measure-CodeQuality.ps1` 100% line and branch, 0 failing members, for the three changed libraries.

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. FTP and NATS clients ending lines with a bare line feed have each line in the exchange log, secret masked, as it arrives (ADR-0182 amendment 31).
