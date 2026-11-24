---
id: BL-1641
title: Cover DictContentDictionary.FindDefinition's unmapped and missing-file branches
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Lookup/Surl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1641 — Cover DictContentDictionary.FindDefinition's unmapped and missing-file branches

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Dict.UnitLibrary` reports 100% line and 100% branch with no failing
member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Protocol.Dict 99.62% line,
  98.98% branch, 1 failing member.
- `DictContentDictionary.FindDefinition(string, CancellationToken)`,
  `Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary/DictContentDictionary.cs:34`, 80% line, 75% branch, Cx 5, CRAP 5.2.
  - Lines 43-44 uncovered: `mapping.IsMapped` (line 42) is never false. No test has a headword that passes
    `IsHeadwordName` and `IsListedFile` but whose `/<word>` request path the content store does not map.
  - Line 47 is 1 of 2: `contentStore.GetFileStatus(mapping)` never returns null. No test has a mapped path with no file
    status, for example a file listed and then deleted before the lookup.
- Both are reachable through the content store's test double (`IContentStore` / the in-memory content file system).

## Acceptance criteria

- [ ] A named `Surl.Protocol.Dict.UnitTests` test makes the content store refuse to map a listed headword's path and
      asserts `FindDefinition` returns null. Through the server, `DEFINE` answers `552 no match`.
- [ ] A named test has a listed, mapped headword whose file status is null and asserts `FindDefinition` returns null.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Protocol.Dict.UnitLibrary` reports `100 | 100`
      with 0 failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
