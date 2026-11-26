---
id: BL-1687
title: Refuse a bundled -K in Surl.Mcp's start_server, such as -sKfile
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1687 — Refuse a bundled -K in Surl.Mcp's start_server, such as -sKfile

## Goal

`start_server` refuses every spelling of `-K`/`--config` in `arguments`, bundled short options included, so no
config file is read.

## Context

Security (ADR-0103 decision 4). Found in BL-1676 and reproduced there: `"arguments": ["-sKsurl.conf"]` answers
`surl: (37) Could not read config file surl.conf`, so the file is opened. `StartServerSafetyCheck.IsConfigFileOption`
only looks at words starting `-K`; `CommandLineParser.ReadShortOptions` reads a bundle (`-sK<file>`, `-vK <file>`).
The check's own parse reads no config file, so it fails, `FindRefusal` returns null and `Surl.Api` then reads the
file: its options reach `surl` with no loopback or root check. Consider also refusing when the check's parse refused
only because a config file could not be read, so a new spelling cannot reopen this.

## Acceptance criteria

- [x] Named tests in `Surl.Mcp.UnitTests` show `-sK<file>`, `-sK` followed by `<file>`, and `-vK<file>` each refused
      with `StartServerSafetyCheck.ConfigFileRule`, with no server started and no file read.
- [x] A named test shows a short bundle whose argument merely contains `K` (such as `-uK:secret`) is not refused as
      a config file.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- Fix: `StartServerSafetyCheck` now parses with `ConfigFileReadRecorder`, an `IConfigFileSource` that reads nothing
  and records that `CommandLineParser` asked for a file; any request is `ConfigFileRule`. The parser itself decides
  what a `-K` is, so a new spelling cannot reopen this.
- Kept the textual `IsConfigFileOption` check beside it, so a `-K` written after a word `surl` refuses is still
  answered with `ConfigFileRule`, as before. No behaviour decision beyond ADR-0103 decision 4, so no ADR change.
- Tests: `StartServerToolTests.Handler_AConfigFileInAShortBundle_IsRefusedBeforeSurlApiCouldReadIt` (failed 4 of 4
  before the fix), `StartServerSafetyCheckTests.FindRefusal_AConfigFileInAShortBundle_IsRefused` and
  `.FindRefusal_AShortBundleWhoseArgumentHoldsK_IsNotRefusedAsAConfigFile`.
- 2026-11-25: build 0 warnings; fast tests 28929 passed, 0 failed, 3 skipped. `dotnet format --verify-no-changes`
  still fails on files this task did not change (`ListKeysTool.cs` final newline in this project among them), which
  BL-1678 and BL-1686 cover.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. start_server refuses -K/--config in every spelling, short bundles included, before any config file is read
