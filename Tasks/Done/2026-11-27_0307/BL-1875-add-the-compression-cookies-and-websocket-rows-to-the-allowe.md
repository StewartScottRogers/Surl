---
id: BL-1875
title: Add the Compression, Cookies and WebSocket rows to the allowed-library isolation test
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1875 — Add the Compression, Cookies and WebSocket rows to the allowed-library isolation test

## Goal

`ProtocolIsolationTests.ForbiddenProtocolReferences_AllowedLibrary_IsNotForbidden` has a `DataRow` for every
horizontal library on the allow-list.

## Context

Found by BL-1836 and checked on 2026-11-27. A test-only change in
`Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`; no production code, so no store, event
or fault changes. The `HorizontalLibraries` dictionary has the keys `Compression`, `Cookies` and `WebSocket`
(ADR-0105, ADR-0189, ADR-0084), but the test's `DataRow` list, which otherwise names every key plus
`Abstractions`, has none for those three. Add them in the dictionary's order. BL-1836 edits comments in the same
file; keep them.

## Acceptance criteria

- [x] The test has `[DataRow(Compression)]`, `[DataRow(Cookies)]` and `[DataRow(WebSocket)]`.
- [x] Every key of `HorizontalLibraries` has exactly one `DataRow` on that test, and the `Abstractions` row stays.
- [x] No other assertion, allow-list entry or `DataRow` in the file changes.
- [x] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitTests -warnaserror` is clean and that project's tests
      pass without `TestCategory=Integration`.

## Notes

- Added the three rows in `HorizontalLibraries` order. The diff is three added lines and nothing else.
- Checked by script: the test's rows are `Abstractions` followed by all 33 dictionary keys, in order.
- Verified: project build clean with `-warnaserror`, its 648 fast tests pass; solution build 0 warnings,
  `dotnet format --verify-no-changes` clean, solution fast tests pass.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The allowed-library isolation test now has a DataRow for every horizontal library, including Compression, Cookies and WebSocket.
