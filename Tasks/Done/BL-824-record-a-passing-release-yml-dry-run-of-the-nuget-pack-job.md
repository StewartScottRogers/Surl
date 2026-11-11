---
id: BL-824
title: Record a passing release.yml dry run of the NuGet pack job
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-643]
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
  `Pack the NuGet packages` job passed.
- `gh run download 43034425678 -n nuget-packages` gave `Surl.Api.0.0.0-dev.427.nupkg` and
  `Surl.Mcp.0.0.0-dev.427.nupkg`.
- The run as a whole failed: `Publish linux-x64` and `Publish linux-arm64` stopped at `Install the native
  toolchain` (exit code 100, apt), and `Conformance against the published linux-x64` then found no artifact. That
  is outside the `pack` job and is already tracked by BL-783, which names this run; `release.yml` was not changed.
- Earlier pull request dry runs 42291259592 and 42278159962 (2026-11-06) also passed the `pack` job; the master
  run was chosen because the goal asks for a run after BL-643 reached `master`.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. release.yml run 43034425678 on master 424106dd passed the pack job; nuget-packages holds Surl.Api and Surl.Mcp 0.0.0-dev.427
