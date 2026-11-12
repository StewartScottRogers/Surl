---
id: BL-684
title: Warn when the --user-file is readable by other users
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-644, BL-613]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-059
created: 2026-11-04
completed: 2026-11-11
---
# BL-684 — Warn when the --user-file is readable by other users

## Goal

On Linux and macOS, `surl --user-file <file>` writes `surl: warning: <file> is readable by other users and may hold
passwords` at every start when the file's Unix mode grants group or other read, as ADR-0101 decision 5 warns for
config files and `--mailbox-file`.

## Context

- Decision: ADR-0101 (BL-641) decision 5, "A file readable by other users": the exact text, start-up warning order
  and level (ADR-0032 section 9, ADR-0033), no warning on Windows.
- Builds on: BL-644, which writes the same warning for `-K` files and should leave a shared helper to call; BL-613,
  which moves `AuthenticationComposition` (where `--user-file` is read today, `Application/Surl.Console/
  AuthenticationComposition.cs`) into `Application/Surl.Composition.UnitLibrary`.
- Tests must stay platform-neutral: the mode check is behind a seam, and the Unix-only path is tested with
  `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` where it touches a real file.

## Acceptance criteria

- [x] A named test in `Application/Surl.Composition.UnitTests` shows the warning written for a user file whose mode
      grants group or other read, and none for mode `0600`, through the mode seam.
- [x] A named test shows no warning on Windows (the seam reports no mode).
- [x] The `--user-file` `AiHelpProse` paragraph and `ManualText` mention the warning.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      touched libraries keep 100% line and branch coverage.

## Notes

- Filed by BL-641 (ADR-0101) as follow-up work; not widened into BL-644, whose `touches` do not include the
  composition.
- BL-644 left no shared helper (it filed the `-K` warning as BL-780), so this task adds it:
  `Surl.Composition`'s public `ReadableByOthersWarning` (`Write` and `ReadDiskMode`), for BL-780 and
  the `--mailbox-file` warning to reuse. No new ADR: ADR-0101 decision 5 already fixes text, level and
  the Windows exemption.
- The mode seam is `SurlServerCompositionSeams.ReadStartFileUnixMode` (`Func<string, UnixFileMode?>`),
  defaulting to `ReadDiskMode`: `null` on Windows and when the mode cannot be read (a host whose
  `ReadStartFile` serves an in-memory file gets no warning rather than a failure). Choice: an unreadable
  mode never fails the start, because the file itself was already read.
- Order: right after the `--fault` warning, before the keytab lines, from info up (ADR-0032 section 9).
- Warns when either group or other read is set (mode `& 0044`), as the ADR says; write or execute bits
  alone do not warn.
- Tests: `ReadableByOthersWarningTests` (Composition) - `Write_*` for the modes, below-info and null,
  `ServeAsync_UserFileReadableByGroup_WritesTheWarningBeforeTheStatusLineThroughTheModeSeam`,
  `ServeAsync_UserFileOwnerOnly_WritesNoWarningThroughTheModeSeam`,
  `ServeAsync_ModeSeamReportsNoModeAsOnWindows_WritesNoWarning`; real-disk `ReadDiskMode_*` tests are
  `Integration` with `OSCondition` per platform. `ReadDiskMode` is `ExcludeFromCodeCoverage` like
  `UnixSocketConnectionListener.MakeOwnerOnly`, since its branch depends on the OS; every other new
  branch is covered by the fast tests. `AiHelpTextTests.Prose_EverySurlLineQuoted_IsOneTheCodeWrites`
  lists the new line.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. --user-file readable by group or others warns at start on Linux and macOS through a mode seam; none on Windows
