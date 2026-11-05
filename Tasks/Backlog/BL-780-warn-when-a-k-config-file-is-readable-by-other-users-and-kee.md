---
id: BL-780
title: Warn when a -K config file is readable by other users and keep the files read on SurlCommandLine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-644]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-780 — Warn when a -K config file is readable by other users and keep the files read on SurlCommandLine

## Goal

On Linux and macOS, `surl -K <file>` whose file mode grants group or other read writes
`surl: warning: <file> is readable by other users and may hold passwords` at start, with the other
start-up warnings and at their level; Windows writes nothing.

## Context

- ADR-0101 decision 5 (last paragraph) and decision 4's model table (`ConfigFiles`). BL-644 built
  `-K`/`--config` but left this out: adding `SurlCommandLine.ConfigFiles` makes
  `Surl.Mcp.UnitTests`'s `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` fail until the
  member is classified in `Surl.Mcp.UnitLibrary`, which was outside BL-644's `touches`.
- Where: `Application/Surl.Cli.UnitLibrary` - add `SurlCommandLine.ConfigFiles` (every path read,
  nested included, `-` for standard input), set by the `config` row in `CommandLineOptions`
  (today `(c, _) => c`). `Application/Surl.Composition.UnitLibrary` - the warning beside the other
  start-up warnings (`File.GetUnixFileMode` behind a seam, as `--user-file` reads go through
  `readStartFile`). `Application/Surl.Mcp.UnitLibrary` - classify the new member.
- BL-684 adds the same warning for `--user-file`; share the text.

## Acceptance criteria

- [ ] `SurlCommandLine.ConfigFiles` lists the files read in order, nested ones included, pinned by a
      named test in `Surl.Cli.UnitTests`.
- [ ] A named test in `Surl.Composition.UnitTests` shows the warning for a group- or other-readable
      mode and none for `0600`, and none on Windows.
- [ ] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [ ] `dotnet build -warnaserror` clean and fast tests green; touched libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
