---
id: BL-081
title: Answer MATCH in Surl.Protocol.Dict whatever --list-directories says
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Dict.UnitLibrary, Surl.Protocol.Dict.UnitTests, Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: FR-016
created: 2026-09-28
completed:
---
# BL-081 — Answer MATCH in Surl.Protocol.Dict whatever --list-directories says

## Goal

`surl dict://…` answers `MATCH ! . hel` with `152 2 matches found` for a served root
holding `hello` and `help` when `--list-directories` is off (the default), as
ADR-0011 section 4 says and BL-033's `match-hel` recording from pinned upstream curl
8.21.0 expects.

## Context

- Found by BL-039's integration test
  `UpstreamCurlQueriesSurlOverDictTests.Query_RecordedCase_ExitsAndPrintsWhatWasRecorded`
  for `match-hel`: pinned upstream curl exits 0 but prints `552 no match` where the
  recording has `152 2 matches found`, `surl "hello"`, `surl "help"`, `.`, `250 ok`.
- Cause: `DictContentDictionary.MatchHeadwords` lists the served root with
  `ContentStore.ListDirectory`, which answers every listing as absent when
  `ContentExposureOptions.ListDirectories` is off. `surl` composes its content store
  with the command line's exposure options, so `--list-directories` is off by default.
  `DictProtocolServerTests` build their `ContentStore` without options, which the
  fast tests never caught.
- ADR-0011 section 4: "`MATCH` lists headwords. It is the protocol's lookup, not a
  directory listing, so it is answered whatever `--list-directories` says; it lists only
  files, never directories" - and dot-files and unfollowed symbolic links stay out, as
  ADR-0006 section 2 gives for every protocol.
- The fix may need a `ContentStore` member that enumerates the root's file entries
  under the dot-file and symbolic-link rules but not the listing switch; name it for
  what it does.

## Acceptance criteria

- [ ] A fast test in `Surl.Protocol.Dict.UnitTests` builds the content store with
      `ListDirectories = false` and asserts `MATCH ! . hel` answers the bytes in
      `Fixtures/match-hel/stdout.bin`.
- [ ] With `ListDirectories = false`, `MATCH` still leaves out dot-files (unless
      `ServeDotFiles`), directories, and symbolic links that are not followed; a fast
      test pins each.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in the changed libraries.
- [ ] The expected bytes are unchanged from BL-033's recording.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Filed by BL-039: pinned upstream curl disagrees with live surl on `match-hel`.
