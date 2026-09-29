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
completed:
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

- [ ] `CommandLineRunner` builds its `ContentStore` with a `ContentExposureOptions` mapped
      from `SurlCommandLine`, and a fast test proves each of the five options reaches it.
- [ ] A fast test proves `surl` with no exposure option serves with
      `new ContentExposureOptions()`'s values.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

Filed by BL-047, which added the options to `Surl.Content` but stayed inside its own
`touches`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
