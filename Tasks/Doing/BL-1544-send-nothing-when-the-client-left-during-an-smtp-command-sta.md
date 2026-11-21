---
id: BL-1544
title: Send nothing when the client left during an SMTP command-stage delay hold
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1540]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary, Protocols/Mail/Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1544 — Send nothing when the client left during an SMTP command-stage delay hold

## Goal

An SMTP command whose `delay` hold ended because the client left is not answered and the session ends; a staying client gets the same bytes as before.

## Context

ADR-0141 Amendment 3 (SmtpSession.Faults.cs), using the BL-1540 helper result.

## Acceptance criteria

- [ ] The goal holds, shown by named unit tests that fail without the change.
- [ ] dotnet build is clean, fast tests pass, 100% line and branch coverage of the changed library.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
