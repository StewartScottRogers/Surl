---
id: BL-1420
title: Withhold the FTP PASS and ACCT arguments from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419]
touches: [Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1420 — Withhold the FTP PASS and ACCT arguments from the exchange log

## Goal

`surl -v --user alice:s3cret ftp://127.0.0.1:0/` (and `ftps://`), answering
`curl -u alice:s3cret`, logs `PASS` and `ACCT` with their arguments in the redacted form BL-1417's ADR
fixes, never `s3cret`.

## Context

- Bug report item: the cookbook saw `PASS s3cret` in the `-v` log, over FTPS as well.
- `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary/FtpCommandResponder.cs:101` (`["PASS"] =
  AnswerPassAsync`) and `:144` (`["ACCT"]`). The control connection is CR LF lines, and FTPS's
  `AUTH TLS` upgrade leaves the logged bytes plaintext (ADR-0033 section 4), so BL-1418's line
  redactor fits.
- Commands are case-insensitive (`pass s3cret`). `USER alice` stays visible unless the ADR says
  otherwise.

## Acceptance criteria

- [ ] The FTP protocol server implements BL-1418's contract. Fast tests in
      `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests` (e.g. `FtpExchangeLogRedactionTests`) show
      `PASS s3cret\r\n` logged in the ADR's form, also when the line is split across two reads, in
      lower case, and after `AUTH TLS`.
- [ ] `ACCT <x>` is redacted the same way; `USER alice` and every other command are logged unchanged.
- [ ] The upstream-test-case mode (`FtpCommandResponder.UpstreamTestCase.cs`) is unaffected:
      `dotnet test Conformance/Surl.Conformance.UnitTests --filter "TestCategory!=Integration"` stays
      green.
- [ ] `dotnet build Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary -warnaserror` is clean, the
      fast tests are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
