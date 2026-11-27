---
id: BL-1798
title: Correct the release statements in the installers, the requirements and the Surl.Mcp README
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [install.sh, install.ps1, Documentation/Product/Requirements.md, Application/Surl.Mcp/README.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1798 — Correct the release statements in the installers, the requirements and the Surl.Mcp README

## Goal

The installer headers, the requirements and the `Surl.Mcp` package README say what is true of
releases and of nuget.org.

## Context

Found by BL-1790, whose `touches` did not name these files. `install.sh` (header, about line 12)
and `install.ps1` (about line 10) say no release has been published; releases are published
(latest `v0.1.11` on 2026-11-26). `Documentation/Product/Requirements.md` (about lines 90 and
141) says no release is tagged. `Application/Surl.Mcp/README.md` presents `dnx Surly.Surl.Mcp`
and `dotnet tool install` as working, but nuget.org held no version on 2026-11-26 (BL-1797);
check the feed again when this runs. That README is packed into the package.

## Acceptance criteria

- [x] None of the four files says no release is published or tagged while a release exists.
- [x] Every statement about the packages on nuget.org matches what the feed holds when the task
      runs.

## Notes

- Measured 2026-11-26: nuget.org's flat container answers 404 for `surly.surl.mcp` and
  `surly.surl.api`, so neither package has a version there.
- Releases: `gh release list` was denied in this run, so GitHub was not queried. The evidence is
  BL-1790's Notes (GitHub API: `v0.1.6` to `v0.1.11` published, `v0.1.11` with both `.nupkg`
  files), local tags from `v0.1.0`, and `release.yml`.
- The installer changes are comments only.
- `Surl.Mcp/README.md`: `dnx` and `dotnet tool install` are now conditional on a package source
  holding the package, with the nuget.org state dated, and the release `.nupkg` with
  `--add-source <folder>` as the route today. That route was not run: the command that packed and
  installed it from a scratch folder was denied.
- Requirements: removed the "Out of scope" row "A managed NuGet API". It said no package is
  shipped; ADR-0098 reversed that (product overview, open question 4). NFR-006 stays `Draft`,
  now for `win-x64` conformance in CI rather than for want of a tag.
- Outside this goal, filed as BL-1800: stale conformance platforms and pass-rate counts. When
  BL-1797 publishes the packages, the dated nuget.org sentence in `Surl.Mcp/README.md` needs
  refreshing too.
- Gates: `dotnet build` 0 warnings, 0 errors; fast tests 0 failed.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The installers, the requirements and the Surl.Mcp README say releases exist and nuget.org holds neither package yet
