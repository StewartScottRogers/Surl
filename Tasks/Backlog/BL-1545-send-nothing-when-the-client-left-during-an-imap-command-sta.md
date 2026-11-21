---
id: BL-1545
title: Send nothing when the client left during an IMAP command-stage delay hold
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1540]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1545 — Send nothing when the client left during an IMAP command-stage delay hold

## Goal

An IMAP command whose `delay` hold ended because the client left is not answered and the session ends; a staying client gets the same bytes as before.

## Context

ADR-0141 Amendment 3 (ImapSession.Faults.cs), using the BL-1540 helper result.

## Acceptance criteria

- [ ] The goal holds, shown by named unit tests that fail without the change.
- [ ] dotnet build is clean, fast tests pass, 100% line and branch coverage of the changed library.

## Notes

## Log

- 2026-11-21: Created.
