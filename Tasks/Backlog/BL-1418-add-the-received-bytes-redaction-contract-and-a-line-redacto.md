---
id: BL-1418
title: Add the received-bytes redaction contract and a line redactor to Surl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1417]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1418 — Add the received-bytes redaction contract and a line redactor to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions` offers the redaction contract BL-1417's ADR names, and a reusable line
redactor that holds received bytes back to a CR LF and rewrites each line a rule marks secret.

## Context

- BL-1417's ADR is the design; this task builds only the contract and the shared line redactor.
  Because it changes the shared contract, it runs apart from every protocol task, as intended (skill,
  "touches").
- Received bytes arrive in arbitrary chunks (`Foundation/Surl.Core.UnitLibrary/RecordingConnection.cs:32`).
  FTP, SMTP, IMAP, POP3 and Redis inline commands are CR LF lines, so one line redactor serves all of
  them; binary protocols (LDAP, MQTT, SOCKS, HTTP/2) implement the contract themselves.
- `Foundation/Surl.Protocol.Abstractions.UnitLibrary` references no other Surl project.
  `ProtocolIsolationTests` in its tests must stay green.

## Acceptance criteria

- [ ] The interfaces BL-1417's ADR names exist in `Foundation/Surl.Protocol.Abstractions.UnitLibrary`,
      with XML doc comments; nothing implements them yet, so no exchange is logged differently.
- [ ] The line redactor (the ADR's name) passes bytes through untouched up to each CR LF and hands each
      whole line to a rule that returns it or its redacted form. Fast tests in
      `Foundation/Surl.Protocol.Abstractions.UnitTests` cover a secret split across chunks, two lines
      in one chunk, a partial line at exchange end, and a line past the ADR's hold-back bound.
- [ ] It never holds back more than the ADR's bound (a test feeds a line longer than the bound with no
      CR LF and checks what was released).
- [ ] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean, the fast
      tests are green, and the new types have 100% line and branch coverage.
- [ ] No reflection and no new package (AOT-safe, base class library only).

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
