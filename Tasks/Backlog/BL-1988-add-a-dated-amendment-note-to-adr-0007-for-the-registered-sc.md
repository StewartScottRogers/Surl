---
id: BL-1988
title: Add a dated amendment note to ADR-0007 for the registered-schemes rule Surl.Console no longer applies
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0007-the-phase-1-command-line-surface.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1988 — Add a dated amendment note to ADR-0007 for the registered-schemes rule Surl.Console no longer applies

## Goal

A reader of ADR-0007 learns, from a dated note beside section 4's rule 3 ("Registered schemes"), where the scheme
refusal is made now.

## Context

Found while working BL-1960 (2026-11-27). Rule 3 says a scheme no registered protocol server claims gets
`UnsupportedProtocol` "from `Surl.Console` before any listener starts". `Surl.Console` makes no such check:
`ListenUrlParser` (`Application/Surl.Cli.UnitLibrary`) refuses it while the command line is read, accepting only
`SchemeDefaultPorts`' schemes (each has a server in `Surl.Composition`'s `ProtocolServerRegistrations.Original`,
held by `CommandLineRunnerTests` in `Application/Surl.Console.UnitTests`) and the extended schemes the caller passes
(ADR-0083, decisions 6 and 7). Recorded text is not rewritten.

## Acceptance criteria

- [ ] A dated note beside rule 3 says `ListenUrlParser` makes the refusal as the command line is read, and names ADR-0083 for the extended schemes.
- [ ] The diff to ADR-0007 only adds lines.

## Notes

## Log

- 2026-11-27: Created.
