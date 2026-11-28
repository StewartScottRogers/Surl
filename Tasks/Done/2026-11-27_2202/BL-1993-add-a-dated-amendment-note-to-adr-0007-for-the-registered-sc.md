---
id: BL-1993
title: Add a dated amendment note to ADR-0007 for the registered-schemes rule Surl.Console no longer applies
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0007-the-phase-1-command-line-surface.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1993 — Add a dated amendment note to ADR-0007 for the registered-schemes rule Surl.Console no longer applies

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

- [x] A dated note beside rule 3 says `ListenUrlParser` makes the refusal as the command line is read, and names ADR-0083 for the extended schemes.
- [x] The diff to ADR-0007 only adds lines.

## Notes

- 2026-11-27: Added the note inside section 4's list item 3, in the "**Amended** (date, task, decided by)" form ADR-0034
  uses. `git diff --numstat` shows 10 lines added, 0 removed.
- 2026-11-27: The note also says `CommandLineParser` holds the refusal until the whole line is read (BL-1372, so a later
  `-s` silences it) and that it replaces section 5's check of every scheme against the registered servers, which is
  stale in the same way. Both checked against `ListenUrlParser.cs` and `CommandLineParser.cs`.
- 2026-11-27: One note in one file, so made in the session rather than delegated to `align-and-document`. No `.cs` or
  project file changed; `dotnet build` was clean and the fast tests passed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0007 section 4 rule 3 now says ListenUrlParser makes the unsupported-scheme refusal, citing ADR-0083 for the extended schemes
