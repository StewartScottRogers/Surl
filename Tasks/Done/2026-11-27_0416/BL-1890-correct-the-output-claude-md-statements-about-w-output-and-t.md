---
id: BL-1890
title: Correct the Output CLAUDE.md statements about -w output and the start-up warnings
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Output.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1890 — Correct the Output CLAUDE.md statements about -w output and the start-up warnings

## Goal

The "Not here" paragraph of `Foundation/Surl.Output.UnitLibrary/CLAUDE.md` says where `-w` output and the start-up
warnings are written.

## Context

Found during BL-1849 (checked against the code on 2026-11-27). Docs only.

- The paragraph (around line 30) ends "A `-w` style line per exchange is not built yet." It is built (BL-1667,
  ADR-0190), outside this library: `-w` / `--write-out` is declared in `Surl.Cli`'s `CommandLineOptions`, expanded
  by `WriteOutFormat.Expand`, and printed to stdout once per exchange by `Surl.Console`'s `WriteOutPrinter`, an
  event stream subscriber. The product overview already says so.
- The same paragraph says `Surl.Console` (`LogStreams`, `CommandLineRunner`) owns the startup warnings and the
  throwaway-certificate note. `Surl.Console` picks the writer (`LogStreams`); `ComposedServing.WriteStartUpLines`
  in `Application/Surl.Composition.UnitLibrary` writes those lines (`WriteThrowawayCertificateLines`).

## Acceptance criteria

- [x] The file no longer says a `-w` style line per exchange is not built.
- [x] It says `-w` output is not in this library and names `WriteOutFormat` (`Surl.Cli`) and `WriteOutPrinter`
      (`Surl.Console`).
- [x] It says `Surl.Console`'s `LogStreams` picks the writer and `Surl.Composition`'s `ComposedServing` writes the
      start-up warnings and the throwaway-certificate note.
- [x] Only that `CLAUDE.md` changes.

## Notes

- Checked against `ComposedServing.cs`, `LogStreams.cs`, `WriteOutPrinter.cs` and `CommandLineOptions.cs` on
  2026-11-27. The edit was made in the session rather than by `align-and-document`: one paragraph, with the facts
  already named by the task. The paragraph is now a three-item list. The `verify` skill was not needed (no `.cs` or
  project file changed); `dotnet build` finished with 0 warnings and the fast tests passed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Output CLAUDE.md now says where -w output and the start-up warnings are written
