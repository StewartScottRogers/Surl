---
id: BL-917
title: Split HttpRequestAnswerer.FileAnswer and StoreUploadAsync under complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1035]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-917 — Split HttpRequestAnswerer.FileAnswer and StoreUploadAsync under complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary` reports no failing member.

## Context

- Found by BL-681 on 2026-11-06 (commit `5ac86cca` plus BL-681): the library is at 100% line and
  branch coverage, but two members it did not touch fail on complexity as the script counts it:
  `HttpRequestAnswerer.FileAnswer` (`HttpRequestAnswerer.cs`, Cx 16) and
  `HttpRequestAnswerer.StoreUploadAsync` (`HttpRequestAnswerer.Uploads.cs`, async, Cx 12).
- The build's `CA1502` passes them; the script counts compiler-generated branches of async state
  machines and pattern matches too, as BL-695's split of the message bus did.
- Behaviour must not change: split into private methods only.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary`
      ends with no failing member, at 100% line and branch coverage.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-11-11 (lane 6): Split made but unverified - `FileAnswer` hands the served 200/206 to a new `ServedFileAnswer`, and `StoreUploadAsync` hands the committed result to a new `CommittedUploadAnswer`. Neither build nor `Measure-CodeQuality.ps1` could run: the pinned .NET SDK 10.0.401 is not installed (only 10.0.100-rc.1 and 11.0.100). Waits on BL-1035; the uncommitted split is left for the shift to stash.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so nothing builds or measures
