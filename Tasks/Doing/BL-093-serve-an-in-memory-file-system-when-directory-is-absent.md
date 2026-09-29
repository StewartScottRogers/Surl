---
id: BL-093
title: Serve an in-memory file system when --directory is absent
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-090, BL-091]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitTests]
requirement: FR-022
created: 2026-09-29
completed:
---
# BL-093 — Serve an in-memory file system when --directory is absent

## Goal

`surl <url>...` without `--directory` serves an `InMemoryContentFileSystem`, empty at
start and gone at exit, and touches no disk; with `--directory <path>` it serves the disk
as today; `--help` states the new default.

## Context

FR-022; ADR-0031 (BL-090) decisions 1 to 4 give the property name and type, the help
line, the missing-path rule and the in-memory served root. This is the breaking change
that supersedes ADR-0007's default of `.`. The Cli and Console changes go together because
making the property nullable does not compile in `Surl.Console` on its own (nullable
warnings are errors).

- `Surl.Cli.UnitLibrary/SurlCommandLine.cs`: `ServedDirectory` is `string` with default
  `"."`; make it the `string?` ADR-0031 names, `null` when `--directory` is absent.
  `CommandLineParser.cs` sets it; `HelpText.cs` line
  `--directory <directory>                 Directory to serve (default: current directory)`
  becomes ADR-0031's line.
- `Surl.Console/CommandLineRunner.cs`: `ServeAsync` calls `canOpenServedDirectory` only
  when a directory was given; `ComposeContentStore` composes
  `new ContentStore(<in-memory root>, new InMemoryContentFileSystem(timeProvider, ...), options)`
  when it is `null`, and `DiskContentFileSystem` otherwise. `ComposeContentStore` is
  static and `ComposeVersionText` calls it with `new SurlCommandLine()`; pass the
  `TimeProvider` it needs explicitly.
- Tests: `Surl.Cli.UnitTests` (parser and help-text tests), `Surl.Console.UnitTests/CommandLineRunnerTests.cs`
  (`ComposeContentStore_ServedDirectory_ServesItsFullPath`,
  `RunAsync_ServedDirectoryCannotBeOpened_...`), with the `FakeListenerFactory`.
- Exposure defaults do not change: uploads still need `--allow-uploads` in memory.
- Of the servers surl composes today only TFTP accepts uploads (`TftpWriteTransfer`); the
  HTTP server refuses `PUT` with `405`, so the end-to-end upload test goes over TFTP.
- `Surl.Conformance.UnitTests/SurlOnLoopback.cs` starts surl in-process and always passes
  `--directory`; `PinnedUpstreamCurl.cs` runs the pinned build.
- Help text is part of the surface upstream curl never sees, so no measurement is needed.

## Acceptance criteria

- [ ] A `Surl.Cli.UnitTests` test proves the parsed directory is `null` without
      `--directory` and the given text with it; the help-text test pins ADR-0031's line.
- [ ] `CommandLineRunnerTests` prove, by name: without `--directory`, the composed store
      uses `InMemoryContentFileSystem` and `canOpenServedDirectory` is never called; with
      `--directory`, it uses `DiskContentFileSystem` over the full path, and a directory
      that cannot be opened still returns `SurlExitCode.CouldNotReadFile` with
      `(37) Could not open directory <path>` (or ADR-0031's replacement rule).
- [ ] A `CommandLineRunnerTests` case composes the store without `--directory` and with
      `--allow-uploads` (as the existing case at line 316 does), uploads through
      `ContentStore` and reads the same bytes back; without `--allow-uploads` the upload
      is refused.
- [ ] An `Integration` conformance test in `UpstreamCurlFetchesFromSurlOverTftpTests`
      starts surl in-process with `--allow-uploads tftp://127.0.0.1:0/` and no
      `--directory` (extend `SurlOnLoopback` with a start that passes no `--directory`),
      has pinned upstream curl 8.21.0 `-T <file> tftp://127.0.0.1:P/up.bin` exit 0, then
      `curl tftp://127.0.0.1:P/up.bin` exit 0 with the same bytes; after surl stops, no
      `up.bin` exists in the test process's current directory, and a fresh in-memory surl
      answers the same fetch with curl's TFTP not-found exit code (measured in the test,
      not assumed).
- [ ] `ADR-0007`'s default is no longer quoted in any XML doc comment in `Surl.Cli.UnitLibrary`
      or `Surl.Console` (`rg -n "current directory" Surl.Cli.UnitLibrary Surl.Console --glob "*.cs"`
      finds nothing that states it as the default).
- [ ] `dotnet build -warnaserror` of `Surl.Cli.UnitLibrary` and `Surl.Console` is clean,
      the fast tests pass, both keep 100% line and branch coverage, and only the
      conformance test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
