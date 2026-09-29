---
id: BL-099
title: Rename ServedDirectoryProbe to DataDirectoryProbe to match the glossary term
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Surl.Console, Surl.Console.UnitTests]
requirement: FR-023
created: 2026-09-29
completed:
---
# BL-099 — Rename ServedDirectoryProbe to DataDirectoryProbe to match the glossary term

## Goal

The class that checks the `--directory` path can be opened is named for the glossary term
ADR-0031 decision 1 chose ("data directory"), not the retired "served directory".

## Context

Found by BL-098. `Documentation/Wiki/Glossary.md` now calls the `--directory` path the
data directory (`SurlCommandLine.DataDirectory`), but `Surl.Console/ServedDirectoryProbe.cs`
and its tests `Surl.Console.UnitTests/ServedDirectoryProbeTests.cs` keep the old name;
references in `Surl.Console/Program.cs`, `Surl.Console/CommandLineRunner.cs` and
`Surl.Console/CLAUDE.md`. ADR-0031 mentions the old name and is not rewritten.

## Acceptance criteria

- [ ] `rg -n "ServedDirectoryProbe" Surl.Console Surl.Console.UnitTests` finds nothing.
- [ ] `rg -n "class DataDirectoryProbe" Surl.Console` finds the class, and
      `Surl.Console.UnitTests/DataDirectoryProbeTests.cs` holds its tests.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
