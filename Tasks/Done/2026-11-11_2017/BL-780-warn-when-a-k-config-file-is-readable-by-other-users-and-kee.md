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
completed: 2026-11-11
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

- [x] `SurlCommandLine.ConfigFiles` lists the files read in order, nested ones included, pinned by a
      named test in `Surl.Cli.UnitTests`.
- [x] A named test in `Surl.Composition.UnitTests` shows the warning for a group- or other-readable
      mode and none for `0600`, and none on Windows.
- [x] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [x] `dotnet build -warnaserror` clean and fast tests green; touched libraries keep 100% line and
      branch coverage.

## Notes

- `SurlCommandLine.ConfigFiles` is filled by the `config` option row before the file is read, so it
  lists files in the order opened (pre-order: a nested file after the file naming it, before that
  file's later lines' files). Pinned by
  `CommandLineParserConfigFileTests.Parse_ConfigFiles_ListsEveryFileReadInOrderNestedIncluded`.
- `ReadableByOthersWarning.WriteForFilesThatMayHoldPasswords` warns each `-K` file in order, then the
  `--user-file`, through the existing `ReadStartFileUnixMode` seam (BL-684's text, shared). Decision:
  `-` (standard input) is never checked - its mode is the pipe's or terminal's, not a file's, so a
  warning would be noise (ADR-0101 decision 5 speaks of files). Tests:
  `WriteForFilesThatMayHoldPasswords_ConfigFileReadableByOthers_WritesTheWarning` (group, other),
  `..._ConfigFileOwnerOnlyOrOnWindows_WritesNothing` (0600, no mode as on Windows),
  `..._ConfigFilesAndUserFile_WarnsEachReadableFileInOrderAndNeverStandardInput`.
- MCP: `ConfigFiles` is classified a path member (`--config`) rather than path-free: `start_server`
  refuses `-K` outright today, so it is always empty there, but it does hold local paths and the root
  check stays right if that refusal ever goes.
- Manual's CONFIG FILES section and the `surl` `--aihelp` paragraph now name the warning.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. SurlCommandLine.ConfigFiles lists every -K file read; each one other users can read is warned at start on Linux and macOS
