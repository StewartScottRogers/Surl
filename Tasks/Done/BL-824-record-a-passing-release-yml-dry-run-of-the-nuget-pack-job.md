---
id: BL-824
title: Record a passing release.yml dry run of the NuGet pack job
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-643, BL-1035]
touches: [.github/workflows/release.yml]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-824 — Record a passing release.yml dry run of the NuGet pack job

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

- [x] A `release.yml` run whose `Pack the NuGet packages` job passed is linked under Notes, with the commit it ran.
- [x] Its `nuget-packages` artifact holds `Surl.Api.<version>.nupkg` and `Surl.Mcp.<version>.nupkg` (checked with
      `gh run download <id> -n nuget-packages`).

## Notes

- Run: https://github.com/StewartScottRogers/Surl/actions/runs/43034425678 (`workflow_dispatch` on `master`,
  2026-11-11, commit `424106dd2db755da45afd990fc5fd075d89c51b7`, which contains BL-643's `1b3fbd4c`). Its
  `Pack the NuGet packages` job passed (rechecked with `gh run view` on lane 6, 2026-11-11).
- `gh run download 43034425678 -n nuget-packages` gave `Surl.Api.0.0.0-dev.427.nupkg` and
  `Surl.Mcp.0.0.0-dev.427.nupkg`.
- The run as a whole failed: `Publish linux-x64` and `Publish linux-arm64` stopped at `Install the native
  toolchain` (apt, exit code 100), outside the `pack` job and tracked by BL-783; `release.yml` was not changed.
- Both boxes are verified. Lane 4 (2026-11-11) had SDK 10.0.404: `dotnet build` clean, fast tests green.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 5 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-824-lane-5-20261111-060243; start with git cherry-pick --no-commit factory/BL-824-lane-5-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; both criteria are already verified (run 43034425678)
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Run 43034425678 pack job green; artifact holds both nupkgs; build clean, fast tests green
