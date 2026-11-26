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
completed:
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

- [ ] A named test for FTP and one for NATS shows a line ended by a bare LF written to the log before any CR LF or flush,
      with its secret masked.
- [ ] A named test shows a protocol that ends lines only at CR LF logging as it does today.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the changed libraries stay at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
