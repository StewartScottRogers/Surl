---
id: BL-785
title: Record a passing release.yml dry run of the NuGet pack job
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-643]
touches: [.github/workflows/release.yml]
requirement: none
created: 2026-11-05
completed:
---
# BL-785 — Record a passing release.yml dry run of the NuGet pack job

## Goal

A `release.yml` run on GitHub (a pull request dry run, the weekly schedule, or `workflow_dispatch`) after BL-643
reached `master` shows the `Pack the NuGet packages` job green and the `nuget-packages` artifact holding
`Surl.Api.<version>.nupkg` and `Surl.Mcp.<version>.nupkg`, and its URL is recorded.

## Context

- BL-643 added the `pack` job to `.github/workflows/release.yml` and proved its steps locally (pack, stamp
  `server.json`, check the packages), but a dark factory lane cannot push, so it could not run the workflow on
  GitHub and record a run URL as its acceptance criterion asked.
- Find a run with `gh run list --workflow release.yml --branch master` (or the shift's pull request); if none ran
  the `pack` job, start one with `gh workflow run release.yml --ref master` - a dry run, publishing nothing.
- If the job fails, fix `release.yml`'s `pack` job (never add `dotnet nuget push` or an API key: publishing needs
  Stewart, root `CLAUDE.md`).

## Acceptance criteria

- [ ] A `release.yml` run whose `Pack the NuGet packages` job passed is linked under Notes, with the commit it ran.
- [ ] Its `nuget-packages` artifact holds `Surl.Api.<version>.nupkg` and `Surl.Mcp.<version>.nupkg` (checked with
      `gh run download <id> -n nuget-packages`).

## Notes

## Log

- 2026-11-05: Created.
