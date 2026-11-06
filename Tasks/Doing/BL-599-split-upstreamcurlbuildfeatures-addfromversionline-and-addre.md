---
id: BL-599
title: Split UpstreamCurlBuildFeatures.AddFromVersionLine and AddRenamedFeatures below complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-599 — Split UpstreamCurlBuildFeatures.AddFromVersionLine and AddRenamedFeatures below complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Conformance.UnitLibrary` reports no failing member.

## Context

- Found by BL-578 on 2026-11-04: `Measure-CodeQuality.ps1` measures `UpstreamCurlBuildFeatures.AddFromVersionLine`
  (`UpstreamCurlBuildFeatures.cs:88`) and `AddRenamedFeatures` (`:110`) at cyclomatic complexity 12, above the
  threshold of 10, at 100% line and branch coverage. The build's `CA1502` passes them, so the script's IL-based
  count is what fails. Both landed with BL-389 (commit 22100843).
- Never raise the threshold; extract private methods with names that say what each part derives.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Conformance.UnitLibrary` reports 0 failing members.
- [ ] `UpstreamCurlBuildFeaturesTests` pass unchanged, `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
