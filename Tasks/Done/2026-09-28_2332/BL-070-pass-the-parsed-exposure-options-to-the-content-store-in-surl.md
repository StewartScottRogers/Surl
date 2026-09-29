---
id: BL-070
title: Pass the parsed exposure options to the content store in Surl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-047]
touches: [Surl.Console, Surl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-070 — Pass the parsed exposure options to the content store in Surl.Console

## Goal

`surl` serves with ADR-0006's exposure defaults - uploads refused, listings refused,
symbolic links not followed, dot-files hidden, uploads capped at 104857600 bytes - and
`--allow-uploads`, `--list-directories`, `--follow-symlinks`, `--serve-dot-files` and
`--max-filesize` change them.

## Context

- `Surl.Cli` already parses all five options into `SurlCommandLine` (`AllowUploads`,
  `ListDirectories`, `FollowSymlinks`, `ServeDotFiles`, `MaxUploadBytes`; ADR-0007's
  option table).
- BL-047 added `ContentExposureOptions` and the constructor
  `ContentStore(string, IContentFileSystem, ContentExposureOptions)`; ADR-0015 records how
  the store applies them.
- `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` still builds the store with
  the two-argument constructor, which serves with
  `ContentExposureOptions.ServeEverythingInsideTheRoot` - listings, dot-files and links on.
  Map `SurlCommandLine` onto a `ContentExposureOptions` and use the three-argument
  constructor.
- Any change in what `surl` answers (an HTTP directory index now answered as absent) is
  measured against pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1` before a
  byte is pinned (ADR-0003).

## Acceptance criteria

- [x] `CommandLineRunner` builds its `ContentStore` with a `ContentExposureOptions` mapped
      from `SurlCommandLine`, and a fast test proves each of the five options reaches it.
- [x] A fast test proves `surl` with no exposure option serves with
      `new ContentExposureOptions()`'s values.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

Filed by BL-047, which added the options to `Surl.Content` but stayed inside its own
`touches`.

Delivered (2026-09-28):
- `CommandLineRunner.ComposeContentStore(SurlCommandLine)` (internal) builds the one store:
  `Path.GetFullPath(ServedDirectory)`, `DiskContentFileSystem`, and a
  `ContentExposureOptions` mapped field for field (`FollowSymlinks` -> `FollowSymbolicLinks`).
  `ComposeProtocolServers` now takes the store. `--version` composes its servers from
  `new SurlCommandLine()`, so it too builds a store with ADR-0006's defaults.
- Test seam (default taken): the store exposes `ExposureOptions`, so the tests parse a real
  command line with `CommandLineParser` and compare `ComposeContentStore(...).ExposureOptions`
  with the expected record - one test per option plus one for no option. No new seam needed.
- No byte pinned: a hidden or unlisted path is the store's "absent", which
  `Surl.Protocol.Http` already answers with its existing not-found response; this task pins
  no wire bytes, so no new `Record-CurlExchange.ps1` measurement was due. The in-process
  conformance tests fetch only regular files and stay green under the new defaults.
- Follow-up filed: BL-071 retires `ContentStore(string, IContentFileSystem)` and the
  `ServeEverythingInsideTheRoot` comment that names its now-gone production callers.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. surl serves with ADR-0006's exposure defaults and the five exposure options reach the content store
