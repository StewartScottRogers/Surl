---
id: BL-917
title: Split HttpRequestAnswerer.FileAnswer and StoreUploadAsync under complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
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

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
