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
completed: 2026-09-29
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

- [x] `ContentStore` has no two-argument constructor, and `dotnet build` is clean.
- [x] Every test that built a store with it passes an explicit `ContentExposureOptions`,
      and the fast tests are green.
- [x] DICT's handling of its database under default options is decided in an ADR and
      pinned by a test in `Surl.Protocol.Dict.UnitTests`.

## Notes

Filed by BL-047.

- 2026-09-29: removed `ContentStore(string, IContentFileSystem)` and its test
  `Constructor_WithoutExposureOptions_ServesEverythingInsideTheRoot`. Every test call site
  (Content, HTTP, Gopher, DICT, TFTP tests) now passes
  `ContentExposureOptions.ServeEverythingInsideTheRoot` explicitly - exactly what the removed
  constructor applied, so no test changed meaning.
- Choice: **kept** `ContentExposureOptions.ServeEverythingInsideTheRoot`. The Context says to
  remove it only if nothing uses it; about seventy test call sites use it as the explicit
  options for tests that exercise something other than the exposure rules. Its doc comment
  now says that, and no longer names a constructor or BL-069.
- DICT: no new ADR. ADR-0011 section 4 already decides that `MATCH` is the protocol's
  lookup, not a directory listing, and BL-081 gave DICT
  `ContentStore.ListDirectoryWhateverTheListingSwitchSays` for it; `DEFINE` reads one file
  and never lists. `Surl.Protocol.Dict.UnitLibrary` needed no change.
  `DictProtocolServerTests.ServeAsync_UnderDefaultExposureOptions_ServesTheDatabase` pins
  `DEFINE` and `MATCH` under `new ContentExposureOptions()`.
- ADR-0015 section 8 still describes the constructor as it stood at BL-047; it is a
  decision record of that moment and outside `touches`, so left as written.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ContentStore has one constructor; every caller passes ContentExposureOptions, and DICT serves its database under default options
