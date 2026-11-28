---
id: BL-1980
title: Say in the README, download page and roadmap that 0.1.12 put both packages on nuget.org
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1797]
touches: [README.md, DOWNLOAD.md, Documentation/Planning/Roadmap.md, Documentation/Product/Product-Overview.md, Application/Surl.Mcp/README.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1980 — Say in the README, download page and roadmap that 0.1.12 put both packages on nuget.org

## Goal

Every document that says `Surly.Surl.Api` or `Surly.Surl.Mcp` is not on nuget.org says instead
that both are, from 0.1.12, and how to install them from there.

## Context

`v0.1.11`'s release job got HTTP 401 at "Log in to nuget.org" because `release.yml` logged in
as the GitHub name `StewartScottRogers`. Commit `c65ad0895` changed it to `StewartRogers86`, the
nuget.org user who created the "Surl releases" trust policy, and the `v0.1.12` release (tag
commit `097e1d8e0`, run 46064723943, 2026-11-27) pushed both packages; nuget.org answered
"Created" for each, and
https://api.nuget.org/v3-flatcontainer/surly.surl.api/index.json and
.../surly.surl.mcp/index.json list `0.1.12`.

Statements still saying otherwise on 2026-11-27: `README.md` lines 543, 549 and 650 ("not on
nuget.org yet", "`dnx Surly.Surl.Mcp` cannot fetch it from there"), `DOWNLOAD.md` lines 42-43,
`Documentation/Planning/Roadmap.md` lines 284-285 and the "Not on nuget.org" item at 304.
Check `Documentation/Product/Product-Overview.md` too. The `surl` command line itself is not a
.NET tool and stays on the GitHub release only; say so where it matters, do not imply otherwise.

## Acceptance criteria

- [x] `README.md`, `DOWNLOAD.md` and `Documentation/Planning/Roadmap.md` state that
      `Surly.Surl.Api` and `Surly.Surl.Mcp` are on nuget.org from 0.1.12, with
      `dotnet add package Surly.Surl.Api` and `dotnet tool install -g Surly.Surl.Mcp` (or
      `dnx Surly.Surl.Mcp`) as the install commands.
- [x] A search of those files and `Documentation/Product/Product-Overview.md` for
      "not on nuget.org" and "cannot fetch it from there" finds nothing.
- [x] The roadmap's "Not on nuget.org" item is removed or rewritten as done, naming
      `v0.1.12` and BL-1797.

## Notes

- Verified 2026-11-27 with `gh run view`: run 46064723943 (`v0.1.12`, `097e1d8e0`) concluded
  success, and its release job's log shows nuget.org answering "Created" for both
  `Surly.Surl.Api.0.1.12.nupkg` and `Surly.Surl.Mcp.0.1.12.nupkg`.
- Added `Application/Surl.Mcp/README.md` to `touches`: it said nuget.org held no version of the
  package, which the Goal covers. No task in Doing named it.
- Also moved the "latest release" statements in `README.md`, `DOWNLOAD.md` and the roadmap from
  `v0.1.11` to `v0.1.12`, so they agree with the nuget.org statements. The README's other dated
  figures were not re-measured.
- Filed BL-1982: ADR-0099 and ADR-0103 amendments name the wrong nuget.org account.
- `dotnet build` clean (0 warnings); fast tests passed, 0 failed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. README, download page, roadmap, product overview and Surl.Mcp README say both packages are on nuget.org from 0.1.12, with install commands
