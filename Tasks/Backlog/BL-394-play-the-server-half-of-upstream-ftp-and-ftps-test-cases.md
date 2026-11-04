---
id: BL-394
title: Play the server half of upstream ftp and ftps test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed:
---
# BL-394 — Play the server half of upstream ftp and ftps test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `ftp`, `ftp-ipv6` or `ftps`. Surl plays
their server half as BL-386's ADR defines, and the pass-rate file counts them.

## Context

- Decision: BL-386's ADR, decision 2, for the FTP family. `touches` assumes the code sits in
  `Surl.Conformance` and `Surl.Protocol.Ftp`. Follow the ADR if it says otherwise.
- FILEFORMAT.md (tag `curl-8_21_0`) lists the FTP `<servercmd>` instructions at lines 367 to 395:
  `REPLY`, `REPLYLF`, `COUNT`, `DELAY`, `RETRWEIRDO`, `RETRNOSIZE`, `RETRSIZE`, `NOSAVE`, `SLOWDOWN`,
  `SLOWDOWNDATA`, `PASVBADIP`, `CAPA`, `AUTH` and `STOR`. The `<size>` and `<mdtm>` reply sections
  apply here too.
- How Surl's FTP server answers: ADR-0052, with the data connection through `IDataConnectionOpener`.
  The runner, checker and pass-rate file are BL-392 and BL-393's.

## Acceptance criteria

- [ ] Unit tests drive the FTP server half with inline cases for each `<servercmd>` the ADR keeps.
      Each excluded instruction gives its named skip reason.
- [ ] The run covers every runnable FTP-family case on Windows. The pass-rate file's rows include
      them, and Notes record the counts.
- [ ] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
