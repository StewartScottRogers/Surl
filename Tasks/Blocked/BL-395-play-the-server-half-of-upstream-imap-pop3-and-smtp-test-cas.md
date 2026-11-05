---
id: BL-395
title: Play the server half of upstream imap, pop3 and smtp test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests, Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests, Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests, Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed:
---
# BL-395 — Play the server half of upstream imap, pop3 and smtp test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `imap`, `pop3` or `smtp`. Surl plays their
server half as BL-386's ADR defines, and the pass-rate file counts them.

## Context

- Decision: BL-386's ADR, decision 2, for the mail family. `touches` assumes the code sits in
  `Surl.Conformance`, the three mail servers and their shared `Surl.LineProtocol` (ADR-0050). Follow the
  ADR if it says otherwise.
- FILEFORMAT.md (tag `curl-8_21_0`) gives the mail family the same `<servercmd>` instructions as FTP
  (lines 367 to 395): `REPLY` (with the `welcome` command), `COUNT`, `DELAY`, `CAPA` and `AUTH`.
- How Surl's mail servers answer: ADR-0049 (SASL and APOP), ADR-0053 (SMTP), ADR-0055 (IMAP), and
  ADR-0056 (POP3). The runner, checker and pass-rate file are BL-392 and BL-393's.

## Acceptance criteria

- [ ] Unit tests drive each mail server half with inline cases for `welcome`, `REPLY`, `CAPA` and
      `AUTH`. Each excluded instruction gives its named skip reason.
- [ ] The run covers every runnable IMAP, POP3 and SMTP case on Windows. The pass-rate file's rows
      include them, and Notes record the counts.
- [ ] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Blocked. Stewart: Surl dark factory timed out after 120 min; see Z:\repos\Surl.logs\BL-395-20261105-054539-L7.jsonl
