---
id: BL-069
title: Pass ContentExposureOptions from every ContentStore caller and remove the two-argument constructor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-047, BL-070]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests, Surl.Protocol.Http.UnitTests, Surl.Protocol.Gopher.UnitTests, Surl.Protocol.Dict.UnitLibrary, Surl.Protocol.Dict.UnitTests, Surl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-069 — Pass ContentExposureOptions from every ContentStore caller and remove the two-argument constructor

## Goal

`ContentStore` has one constructor, `ContentStore(string, IContentFileSystem,
ContentExposureOptions)`, so no caller can build a store that skips ADR-0006's exposure
rules by leaving the options out.

## Context

- BL-047 kept `ContentStore(string, IContentFileSystem)`, serving with
  `ContentExposureOptions.ServeEverythingInsideTheRoot`, because the HTTP, Gopher, DICT and
  TFTP servers' tests and `Surl.Console` built stores with it while other lanes were
  working in them (ADR-0015).
- Each caller's tests pass the options they rely on, e.g. Gopher's menu tests need
  `ListDirectories = true`, and DICT's tests the options its database needs.
- `Surl.Protocol.Dict.UnitLibrary/DictContentDictionary.cs` reads its database with
  `ContentStore.ListDirectory` of the root. Under default options that listing is answered
  as absent, so DICT would serve an empty database. Decide (in ADR-0011's successor or a
  new ADR) whether DICT's own read of its database is a listing a peer asked for; if not,
  give it a store read that is not subject to `ListDirectories`.
- Remove `ContentExposureOptions.ServeEverythingInsideTheRoot` too if nothing uses it.

## Acceptance criteria

- [ ] `ContentStore` has no two-argument constructor, and `dotnet build` is clean.
- [ ] Every test that built a store with it passes an explicit `ContentExposureOptions`,
      and the fast tests are green.
- [ ] DICT's handling of its database under default options is decided in an ADR and
      pinned by a test in `Surl.Protocol.Dict.UnitTests`.

## Notes

Filed by BL-047.

## Log

- 2026-09-28: Created.
